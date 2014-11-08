// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.SqlServer.Metadata;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.Tests
{
    public class SqlServerMigrationOperationSqlGeneratorTest
    {
        [Fact]
        public void Generate_when_create_database_operation()
        {
            Assert.Equal(
                @"CREATE DATABASE [MyDatabase]",
                Generate(new CreateDatabaseOperation("MyDatabase")).Sql);
        }

        [Fact]
        public void Generate_when_drop_database_operation()
        {
            Assert.Equal(
                @"DROP DATABASE [MyDatabase]",
                Generate(new DropDatabaseOperation("MyDatabase")).Sql);
        }

        [Fact]
        public void Generate_when_create_sequence_operation()
        {
            Assert.Equal(
                @"CREATE SEQUENCE [dbo].[MySequence] AS bigint START WITH 0 INCREMENT BY 1",
                Generate(new CreateSequenceOperation("dbo.MySequence", 0, 1)).Sql);
        }

        [Fact]
        public void Generate_when_move_sequence_operation()
        {
            Assert.Equal(
                @"ALTER SCHEMA [dbo2] TRANSFER [dbo].[MySequence]",
                Generate(new MoveSequenceOperation("dbo.MySequence", "dbo2")).Sql);
        }

        [Fact]
        public void Generate_when_rename_sequence_operation()
        {
            Assert.Equal(
                @"EXECUTE sp_rename @objname = N'dbo.MySequence', @newname = N'MySequence2', @objtype = N'OBJECT'",
                Generate(new RenameSequenceOperation("dbo.MySequence", "MySequence2")).Sql);
        }

        [Fact]
        public void Generate_when_drop_sequence_operation()
        {
            Assert.Equal(
                @"DROP SEQUENCE [dbo].[MySequence]",
                Generate(new DropSequenceOperation("dbo.MySequence")).Sql);
        }

        [Fact]
        public void Generate_when_alter_sequence_operation()
        {
            Assert.Equal(
                @"ALTER SEQUENCE [dbo].[MySequence] INCREMENT BY 7",
                Generate(new AlterSequenceOperation("dbo.MySequence", 7)).Sql);
        }

        [Fact]
        public void Generate_when_create_table_operation()
        {
            var model = new Entity.Metadata.Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("E",
                b =>
                {
                    b.Property<int>("Foo").ForRelational().DefaultValue(5);
                    b.Property<int>("Foo").Metadata.IsNullable = false;
                    b.Property<int>("Bar");
                    b.Property<int>("Bar").Metadata.IsNullable = true;
                    b.ForRelational().Table("MyTable", "dbo");
                    b.Key("Foo", "Bar").ForRelational().Name("MyPK");
                });

            var operation = OperationFactory().CreateTableOperation(model.GetEntityType("E"));

            Assert.Equal(
                @"CREATE TABLE [dbo].[MyTable] (
    [Foo] int NOT NULL DEFAULT 5,
    [Bar] int,
    CONSTRAINT [MyPK] PRIMARY KEY NONCLUSTERED ([Foo], [Bar])
)",
                Generate(operation, model).Sql);
        }

        [Fact]
        public void Generate_when_create_table_operation_with_Identity_key()
        {
            var model = new Entity.Metadata.Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("E",
                b =>
                {
                    b.Property<int>("Foo").GenerateValueOnAdd().Metadata.IsNullable = false;
                    b.Property<int>("Bar").Metadata.IsNullable = true;
                    b.ForRelational().Table("MyTable", "dbo");
                    b.Key("Foo").ForRelational().Name("MyPK");
                });

            var operation = OperationFactory().CreateTableOperation(model.GetEntityType("E"));

            Assert.Equal(
                @"CREATE TABLE [dbo].[MyTable] (
    [Foo] int NOT NULL IDENTITY,
    [Bar] int,
    CONSTRAINT [MyPK] PRIMARY KEY NONCLUSTERED ([Foo])
)",
                Generate(operation, model).Sql);
        }

        [Fact]
        public void Generate_when_drop_table_operation()
        {
            Assert.Equal(
                @"DROP TABLE [dbo].[MyTable]",
                Generate(new DropTableOperation("dbo.MyTable")).Sql);
        }

        [Fact]
        public void Generate_when_rename_table_operation()
        {
            Assert.Equal(
                @"EXECUTE sp_rename @objname = N'dbo.MyTable', @newname = N'MyTable2', @objtype = N'OBJECT'",
                Generate(new RenameTableOperation("dbo.MyTable", "MyTable2")).Sql);
        }

        [Fact]
        public void Generate_when_move_table_operation()
        {
            Assert.Equal(
                @"ALTER SCHEMA [dbo2] TRANSFER [dbo].[MyTable]",
                Generate(new MoveTableOperation("dbo.MyTable", "dbo2")).Sql);
        }

        [Fact]
        public void Generate_when_add_column_operation()
        {
            var model = new Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("E",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("Bar").ForSqlServer().DefaultValue(5);
                        b.Property<int>("Bar").Metadata.IsNullable = false;
                        b.ForRelational().Table("MyTable", "dbo");
                        b.Key("Id");
                    });

            var operation = OperationFactory().AddColumnOperation(model.GetEntityType("E").GetProperty("Bar"));

            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] ADD [Bar] int NOT NULL DEFAULT 5",
                Generate(operation, model).Sql);
        }

        [Fact]
        public void Generate_when_drop_column_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] DROP COLUMN [Foo]",
                Generate(new DropColumnOperation("dbo.MyTable", "Foo"), new Model()).Sql);
        }

        [Fact]
        public void Generate_when_alter_column_operation()
        {
            var model = new Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("E",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<int>("Foo").Metadata.IsNullable = false;
                    b.ForRelational().Table("MyTable", "dbo");
                    b.Key("Id");
                });

            var operation = OperationFactory().AlterColumnOperation(
                model.GetEntityType("E").GetProperty("Foo"), isDestructiveChange: false);

            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] ALTER COLUMN [Foo] int NOT NULL",
                Generate(operation, model).Sql);
        }

        [Fact]
        public void Generate_when_add_default_constraint_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] ADD CONSTRAINT [DF_dbo.MyTable_Foo] DEFAULT 5 FOR [Foo]",
                Generate(
                    new AddDefaultConstraintOperation("dbo.MyTable", "Foo", 5, null)).Sql);
        }

        [Fact]
        public void Generate_when_drop_default_constraint_operation()
        {
            Assert.Equal(
                @"DECLARE @var0 nvarchar(128)
SELECT @var0 = name FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MyTable') AND COL_NAME(parent_object_id, parent_column_id) = N'Foo'
EXECUTE('ALTER TABLE [dbo].[MyTable] DROP CONSTRAINT ""' + @var0 + '""')",
                Generate(
                    new DropDefaultConstraintOperation("dbo.MyTable", "Foo")).Sql);
        }

        [Fact]
        public void Generate_when_rename_column_operation()
        {
            Assert.Equal(
                @"EXECUTE sp_rename @objname = N'dbo.MyTable.Foo', @newname = N'Foo2', @objtype = N'COLUMN'",
                Generate(
                    new RenameColumnOperation("dbo.MyTable", "Foo", "Foo2")).Sql);
        }

        [Fact]
        public void Generate_when_add_primary_key_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] ADD CONSTRAINT [MyPK] PRIMARY KEY NONCLUSTERED ([Foo], [Bar])",
                Generate(
                    new AddPrimaryKeyOperation("dbo.MyTable", "MyPK", new[] { "Foo", "Bar" }, isClustered: false)).Sql);
        }

        [Fact]
        public void Generate_when_drop_primary_key_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] DROP CONSTRAINT [MyPK]",
                Generate(new DropPrimaryKeyOperation("dbo.MyTable", "MyPK")).Sql);
        }

        [Fact]
        public void Generate_when_add_foreign_key_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable] ADD CONSTRAINT [MyFK] FOREIGN KEY ([Foo], [Bar]) REFERENCES [dbo].[MyTable2] ([Foo2], [Bar2]) ON DELETE CASCADE",
                Generate(
                    new AddForeignKeyOperation("dbo.MyTable", "MyFK", new[] { "Foo", "Bar" },
                        "dbo.MyTable2", new[] { "Foo2", "Bar2" }, cascadeDelete: true)).Sql);
        }

        [Fact]
        public void Generate_when_drop_foreign_key_operation()
        {
            Assert.Equal(
                @"ALTER TABLE [dbo].[MyTable2] DROP CONSTRAINT [MyFK]",
                Generate(new DropForeignKeyOperation("dbo.MyTable2", "MyFK")).Sql);
        }

        [Fact]
        public void Generate_when_create_index_operation()
        {
            Assert.Equal(
                @"CREATE UNIQUE CLUSTERED INDEX [MyIndex] ON [dbo].[MyTable] ([Foo], [Bar])",
                Generate(
                    new CreateIndexOperation("dbo.MyTable", "MyIndex", new[] { "Foo", "Bar" },
                        isUnique: true, isClustered: true)).Sql);
        }

        [Fact]
        public void Generate_when_drop_index_operation()
        {
            Assert.Equal(
                @"DROP INDEX [MyIndex] ON [dbo].[MyTable]",
                Generate(new DropIndexOperation("dbo.MyTable", "MyIndex")).Sql);
        }

        [Fact]
        public void Generate_when_rename_index_operation()
        {
            Assert.Equal(
                @"EXECUTE sp_rename @objname = N'dbo.MyTable.MyIndex', @newname = N'MyIndex2', @objtype = N'INDEX'",
                Generate(
                    new RenameIndexOperation("dbo.MyTable", "MyIndex", "MyIndex2")).Sql);
        }

        [Fact]
        public void GenerateDataType_for_string_thats_not_a_key()
        {
            Assert.Equal("nvarchar(max)", GenerateDataType<string>());
        }

        [Fact]
        public void GenerateDataType_for_string_key()
        {
            Assert.Equal("nvarchar(128)", GenerateDataType<string>(isKey: true));
        }

        [Fact]
        public void GenerateDataType_for_DateTime()
        {
            Assert.Equal("datetime2", GenerateDataType<DateTime>());
        }

        [Fact]
        public void GenerateDataType_for_decimal()
        {
            Assert.Equal("decimal(18, 2)", GenerateDataType<decimal>());
        }

        [Fact]
        public void GenerateDataType_for_Guid()
        {
            Assert.Equal("uniqueidentifier", GenerateDataType<Guid>());
        }

        [Fact]
        public void GenerateDataType_for_bool()
        {
            Assert.Equal("bit", GenerateDataType<bool>());
        }

        [Fact]
        public void GenerateDataType_for_byte()
        {
            Assert.Equal("tinyint", GenerateDataType<byte>());
        }

        [Fact]
        public void GenerateDataType_for_char()
        {
            Assert.Equal("int", GenerateDataType<int>());
        }

        [Fact]
        public void GenerateDataType_for_double()
        {
            Assert.Equal("float", GenerateDataType<double>());
        }

        [Fact]
        public void GenerateDataType_for_short()
        {
            Assert.Equal("smallint", GenerateDataType<short>());
        }

        [Fact]
        public void GenerateDataType_for_long()
        {
            Assert.Equal("bigint", GenerateDataType<long>());
        }

        [Fact]
        public void GenerateDataType_for_sbyte()
        {
            Assert.Equal("smallint", GenerateDataType<sbyte>());
        }

        [Fact]
        public void GenerateDataType_for_float()
        {
            Assert.Equal("real", GenerateDataType<float>());
        }

        [Fact]
        public void GenerateDataType_for_ushort()
        {
            Assert.Equal("int", GenerateDataType<ushort>());
        }

        [Fact]
        public void GenerateDataType_for_uint()
        {
            Assert.Equal("bigint", GenerateDataType<uint>());
        }

        [Fact]
        public void GenerateDataType_for_ulong()
        {
            Assert.Equal("numeric(20, 0)", GenerateDataType<ulong>());
        }

        [Fact]
        public void GenerateDataType_for_DateTimeOffset()
        {
            Assert.Equal("datetimeoffset", GenerateDataType<DateTimeOffset>());
        }

        [Fact]
        public void GenerateDataType_for_byte_array_that_is_not_a_concurrency_token_or_a_primary_key()
        {
            Assert.Equal("varbinary(max)", GenerateDataType<byte[]>());
        }

        [Fact]
        public void GenerateDataType_for_byte_array_key()
        {
            Assert.Equal("varbinary(128)", GenerateDataType<byte[]>(isKey: true));
        }

        [Fact]
        public void GenerateDataType_for_byte_array_concurrency_token()
        {
            Assert.Equal("rowversion", GenerateDataType<byte[]>(isKey: false, isConcurrencyToken: true));
        }

        private static string GenerateDataType<T>(bool isKey = false, bool isConcurrencyToken = false)
        {
            var model = new Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("E",
                b =>
                {
                    b.Property<T>("P");
                    if (isKey)
                    {
                        b.Key("P");
                    }
                    if (isConcurrencyToken)
                    {
                        b.Property<T>("P").Metadata.IsConcurrencyToken = true;
                    }
                });

            var property = model.GetEntityType("E").GetProperty("P");
            var sqlGenerator = CreateSqlGenerator();

            sqlGenerator.TargetModel = property.EntityType.Model;

            return sqlGenerator.GenerateDataType(
                new SchemaQualifiedName(
                    property.EntityType.SqlServer().Table,
                    property.EntityType.SqlServer().Schema),
                OperationFactory().Column(property));
        }

        [Fact]
        public void Delimit_identifier()
        {
            var sqlGenerator = CreateSqlGenerator();

            Assert.Equal("[foo[]]bar]", sqlGenerator.DelimitIdentifier("foo[]bar"));
        }

        [Fact]
        public void Escape_identifier()
        {
            var sqlGenerator = CreateSqlGenerator();

            Assert.Equal("foo[]]]]bar", sqlGenerator.EscapeIdentifier("foo[]]bar"));
        }

        [Fact]
        public void Delimit_literal()
        {
            var sqlGenerator = CreateSqlGenerator();

            Assert.Equal("'foo''bar'", sqlGenerator.DelimitLiteral("foo'bar"));
        }

        [Fact]
        public void Escape_literal()
        {
            var sqlGenerator = CreateSqlGenerator();

            Assert.Equal("foo''bar", sqlGenerator.EscapeLiteral("foo'bar"));
        }

        private static SqlStatement Generate(MigrationOperation migrationOperation, IModel targetModel = null)
        {
            return CreateSqlGenerator(targetModel).Generate(migrationOperation);
        }

        private static SqlServerMigrationOperationSqlGenerator CreateSqlGenerator(IModel targetModel = null)
        {
            return
                new SqlServerMigrationOperationSqlGenerator(
                    new RelationalNameGenerator(
                        new SqlServerMetadataExtensionProvider()), 
                    new SqlServerTypeMapper())
                    {
                        TargetModel = targetModel ?? new Model()
                    };
        }

        private static MigrationOperationFactory OperationFactory()
        {
            var extensionProvider = new RelationalMetadataExtensionProvider();
            var nameGenerator = new RelationalNameGenerator(extensionProvider);

            return new MigrationOperationFactory(extensionProvider, nameGenerator);
        }
    }
}
