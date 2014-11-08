// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Xunit;

namespace Microsoft.Data.Entity.SQLite.Tests
{
    public class SQLiteMigrationOperationSqlGeneratorTest
    {
        [Fact]
        public void Generate_with_create_database_not_supported()
        {
            var operation = new CreateDatabaseOperation("Bronies");

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_drop_database_not_supported()
        {
            var operation = new DropDatabaseOperation("Bronies");

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_create_sequence_not_supported()
        {
            var operation = new CreateSequenceOperation("EpisodeSequence", 0, 1);

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_drop_sequence_is_not_supported()
        {
            var operation = new DropSequenceOperation("EpisodeSequence");

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_move_sequence_is_not_supported()
        {
            var operation = new RenameSequenceOperation("EpisodeSequence", "RenamedSchema");

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_alter_sequence_is_not_supported()
        {
            var operation = new AlterSequenceOperation("EpisodeSequence", 7);

            Assert.Equal(
                Strings.MigrationOperationNotSupported(typeof(SQLiteMigrationOperationSqlGenerator), operation.GetType()),
                Assert.Throws<NotSupportedException>(() => Generate(operation)).Message);
        }

        [Fact]
        public void Generate_with_create_table_with_unique_constraints()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T",
                b =>
                    {
                        b.Property<long>("Id");
                        b.Property<int>("C1");
                        b.Property<string>("C2");
                        b.Key("Id").ForRelational().Name("PK");
                        b.Key("C1", "C2").ForRelational().Name("UC0");
                        b.Key("C2").ForRelational().Name("UC1");
                    });

            var operation = OperationFactory().CreateTableOperation(modelBuilder.Model.GetEntityType("T"));
            var sql = Generate(operation, modelBuilder.Model);

            Assert.Equal(
                @"CREATE TABLE ""T"" (
    ""Id"" INTEGER,
    ""C1"" INT,
    ""C2"" CHAR,
    CONSTRAINT ""PK"" PRIMARY KEY (""Id""),
    UNIQUE (""Id"", ""C1""),
    UNIQUE (""C2"")
)",
                sql);
        }

        [Fact]
        public void Generate_with_create_table_generates_fks()
        {
            var model = new Model();
            var modelBuilder = new BasicModelBuilder(model);
            modelBuilder.Entity("Pegasus",
                b =>
                {
                    b.Property<long>("Id");
                    b.Key("Id");
                });
            modelBuilder.Entity("Friendship",
                b =>
                {
                    b.Property<long>("Friend1Id");
                    b.Property<long>("Friend2Id");
                    b.Key("Friend1Id", "Friend2Id").ForRelational().Name("PegasusPK");
                    b.ForeignKey("Pegasus", "Friend1Id").ForRelational().Name("FriendshipFK1");
                    b.ForeignKey("Pegasus", "Friend2Id").ForRelational().Name("FriendshipFK2");
                });

            var operation = OperationFactory().CreateTableOperation(model.GetEntityType("Friendship"));

            var sql = Generate(operation, model);

            Assert.Equal(
                @"CREATE TABLE ""Friendship"" (
    ""Friend1Id"" INTEGER,
    ""Friend2Id"" INTEGER,
    CONSTRAINT ""PegasusPK"" PRIMARY KEY (""Friend1Id"", ""Friend2Id""),
    CONSTRAINT ""FriendshipFK1"" FOREIGN KEY (""Friend1Id"") REFERENCES ""Pegasus"" (""Id""),
    CONSTRAINT ""FriendshipFK2"" FOREIGN KEY (""Friend2Id"") REFERENCES ""Pegasus"" (""Id"")
)",
                sql);
        }

        [Fact]
        public void Generate_with_rename_table_works()
        {
            var operation = new RenameTableOperation("my.Pegasus", "Pony");

            var sql = Generate(operation);

            Assert.Equal("ALTER TABLE \"my.Pegasus\" RENAME TO \"my.Pony\"", sql);
        }

        [Fact]
        public void Generate_with_move_table_works()
        {
            var operation = new MoveTableOperation("my.Pony", "bro");

            var sql = Generate(operation);

            Assert.Equal("ALTER TABLE \"my.Pony\" RENAME TO \"bro.Pony\"", sql);
        }

        [Fact]
        public void Generate_with_drop_index_works()
        {
            var operation = new DropIndexOperation("Pony", "Ponydex");

            var sql = Generate(operation);

            Assert.Equal("DROP INDEX \"Ponydex\"", sql);
        }

        [Fact]
        public void GenerateLiteral_with_byte_array_works()
        {
            var bros = new byte[] { 0xB2, 0x05 };

            var sql = CreateGenerator().GenerateLiteral(bros);

            Assert.Equal("X'B205'", sql);
        }

        [Fact]
        public void DelimitIdentifier_with_schema_qualified_name_works_when_no_schema()
        {
            var name = new SchemaQualifiedName("Pony");

            var sql = CreateGenerator().DelimitIdentifier(name);

            Assert.Equal("\"Pony\"", sql);
        }

        [Fact]
        public void DelimitIdentifier_with_schema_qualified_name_concatenates_parts_when_schema()
        {
            var name = new SchemaQualifiedName("Pony", "my");

            var sql = CreateGenerator().DelimitIdentifier(name);

            Assert.Equal("\"my.Pony\"", sql);
        }

        private static string Generate(MigrationOperation operation, IModel targetModel = null)
        {
            return CreateGenerator(targetModel).Generate(operation).Sql;
        }

        private static SQLiteMigrationOperationSqlGenerator CreateGenerator(IModel targetModel = null)
        {
            return new SQLiteMigrationOperationSqlGenerator(
                new RelationalNameGenerator(
                    new RelationalMetadataExtensionProvider()), 
                new SQLiteTypeMapper())
                {
                    TargetModel = targetModel ?? new Model(),
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
