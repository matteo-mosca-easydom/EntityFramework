// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Xunit;

namespace Microsoft.Data.Entity.SQLite.Tests
{
    public class SQLiteMigrationOperationPreProcessorTest
    {
        [Fact]
        public void Visit_with_create_table_operation()
        {
            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Key("Id");
                });

            var operation = OperationFactory().CreateTableOperation(targetModelBuilder.Entity("T").Metadata);
            operation.Columns.Add(new Column("Id", typeof(int)));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(operation);

            var operations = PreProcess(new Model(), targetModelBuilder.Model, operationCollection);

            Assert.Equal(1, operations.Count);
            Assert.IsType<CreateTableOperation>(operations[0]);

            var createTableOperation = (CreateTableOperation)operations[0];
            Assert.Equal("T", createTableOperation.TableName);
            Assert.Equal(new[] { "Id" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
        }

        [Fact]
        public void Visit_with_create_table_operation_followed_by_add_foreign_key_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T1",
                b =>
                {
                    b.Property<int>("Id");
                    b.Key("Id");
                });
            targetModelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("C");
                        b.Key("Id");
                        b.ForeignKey("T1", "C").ForRelational().Name("FK");
                    });

            var createTableOperation = OperationFactory().CreateTableOperation(targetModelBuilder.Entity("T2").Metadata);
            var addForeignKeyOperation = createTableOperation.ForeignKeys[0];

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(createTableOperation);
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(sourceModelBuilder.Model, targetModelBuilder.Model, operationCollection);

            Assert.Equal(1, operations.Count);

            Assert.Same(createTableOperation, createTableOperation);
        }

        [Fact]
        public void Visit_with_create_table_operation_followed_by_create_index_operation()
        {
            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<int>("C");
                    b.Key("Id");
                    b.Index("C").ForRelational().Name("IX");
                });

            var createTableOperation = OperationFactory().CreateTableOperation(targetModelBuilder.Entity("T").Metadata);
            var createIndexOperation = OperationFactory().CreateIndexOperation(targetModelBuilder.Entity("T").Metadata.Indexes[0]);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(createTableOperation);
            operationCollection.Add(createIndexOperation);

            var operations = PreProcess(new Model(), targetModelBuilder.Model, operationCollection);

            Assert.Equal(2, operations.Count);

            Assert.Same(createTableOperation, operations[0]);
            Assert.Same(createIndexOperation, operations[1]);
        }

        [Fact]
        public void Visit_with_supported_table_subordinate_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<string>("P");
                    b.Key("Id");
                });

            var operation = OperationFactory().AddColumnOperation(targetModelBuilder.Entity("T").Metadata.GetProperty("P"));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(operation);

            var operations = PreProcess(sourceModelBuilder.Model, targetModelBuilder.Model, operationCollection);

            Assert.Equal(1, operations.Count);
            Assert.Same(operation, operations[0]);
        }

        [Fact]
        public void Visit_with_rename_operation_followed_by_supported_table_subordinate_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.ForRelational().Table("T", "dbo");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<string>("P");
                    b.Key("Id");
                    // TODO: SQLite-specific. Issue #875
                    b.ForRelational().Table("T2", "dbo2");
                });

            var moveTableOperation = OperationFactory().MoveTableOperation("dbo.T", "dbo2");
            var renameTableOperation = OperationFactory().RenameTableOperation("dbo2.T", "T2");
            var addColumnOperation = OperationFactory().AddColumnOperation(targetModelBuilder.Entity("T").Metadata.GetProperty("P"));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(moveTableOperation);
            operationCollection.Add(renameTableOperation);
            operationCollection.Add(addColumnOperation);

            var operations = PreProcess(sourceModelBuilder.Model, targetModelBuilder.Model, operationCollection);

            Assert.Equal(3, operations.Count);
            Assert.Same(moveTableOperation, operations[0]);
            Assert.Same(renameTableOperation, operations[1]);
            Assert.Same(addColumnOperation, operations[2]);
        }

        [Fact]
        public void Visit_with_unsupported_table_subordinate_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });
            sourceModelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("C");
                        b.Key("Id");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T1",
                b =>
                {
                    b.Property<int>("Id");
                    b.Key("Id");
                });
            targetModelBuilder.Entity("T2",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<int>("C");
                    b.Key("Id");
                    b.ForeignKey("T1", "C");
                });

            var addForeignKeyOperation = OperationFactory().AddForeignKeyOperation(
                targetModelBuilder.Entity("T2").Metadata.ForeignKeys[0]);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(sourceModelBuilder.Model, sourceModelBuilder.Model, operationCollection);

            Assert.Equal(4, operations.Count);
            Assert.IsType<RenameTableOperation>(operations[0]);
            Assert.IsType<CreateTableOperation>(operations[1]);
            Assert.IsType<CopyDataOperation>(operations[2]);
            Assert.IsType<DropTableOperation>(operations[3]);

            var renameTableOperation = (RenameTableOperation)operations[0];

            Assert.Equal("T2", renameTableOperation.TableName);
            Assert.Equal("__mig_tmp__T2", renameTableOperation.NewTableName);

            var createTableOperation = (CreateTableOperation)operations[1];

            Assert.Equal("T2", createTableOperation.TableName);
            Assert.Equal(new[] { "Id", "C" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int), typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
            Assert.Equal(1, createTableOperation.ForeignKeys.Count);
            Assert.Equal("FK", createTableOperation.ForeignKeys[0].ForeignKeyName);
            Assert.Equal("T1", createTableOperation.ForeignKeys[0].ReferencedTableName);
            Assert.Equal(new[] { "C" }, createTableOperation.ForeignKeys[0].ColumnNames);
            Assert.Equal(new[] { "Id" }, createTableOperation.ForeignKeys[0].ReferencedColumnNames);

            var copyDataOperation = (CopyDataOperation)operations[2];

            Assert.Equal("__mig_tmp__T2", copyDataOperation.SourceTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.SourceColumnNames);
            Assert.Equal("T2", copyDataOperation.TargetTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.TargetColumnNames);

            var dropTableOperation = (DropTableOperation)operations[3];

            Assert.Equal("__mig_tmp__T2", dropTableOperation.TableName);
        }

        [Fact]
        public void Visit_with_rename_operation_followed_by_unsupported_subordinate_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });
            sourceModelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("P");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.ForRelational().Table("T", "dbo");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T1",
                b =>
                {
                    b.Property<int>("Id");
                    b.Key("Id");
                });
            targetModelBuilder.Entity("T2",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<int>("P");
                    b.Key("Id");
                    b.ForeignKey("T1", "P");
                    // TODO: SQLite-specific. Issue #875
                    b.ForRelational().Table("T2", "dbo2");
                });

            var moveTableOperation = OperationFactory().MoveTableOperation("dbo.T", "dbo2");
            var renameTableOperation = OperationFactory().RenameTableOperation("dbo2.T", "T2");
            var addForeignKeyOperation = OperationFactory().AddForeignKeyOperation(targetModelBuilder.Entity("T2").Metadata.ForeignKeys[0]);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(moveTableOperation);
            operationCollection.Add(renameTableOperation);
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(sourceModelBuilder.Model, targetModelBuilder.Model, operationCollection);

            Assert.Equal(3, operations.Count);
            Assert.IsType<CreateTableOperation>(operations[0]);
            Assert.IsType<CopyDataOperation>(operations[1]);
            Assert.IsType<DropTableOperation>(operations[2]);

            var createTableOperation = (CreateTableOperation)operations[0];

            Assert.Equal("dbo2.T2", createTableOperation.TableName);
            Assert.Equal(new[] { "Id", "C" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int), typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
            Assert.Equal(1, createTableOperation.ForeignKeys.Count);
            Assert.Equal("FK", createTableOperation.ForeignKeys[0].ForeignKeyName);
            Assert.Equal("T1", createTableOperation.ForeignKeys[0].ReferencedTableName);
            Assert.Equal(new[] { "C" }, createTableOperation.ForeignKeys[0].ColumnNames);
            Assert.Equal(new[] { "Id" }, createTableOperation.ForeignKeys[0].ReferencedColumnNames);

            var copyDataOperation = (CopyDataOperation)operations[1];

            Assert.Equal("dbo.T", copyDataOperation.SourceTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.SourceColumnNames);
            Assert.Equal("dbo2.T2", copyDataOperation.TargetTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.TargetColumnNames);

            var dropTableOperation = (DropTableOperation)operations[2];

            Assert.Equal("dbo.T", dropTableOperation.TableName);
        }

        [Fact]
        public void Visit_with_rename_index_operation()
        {
            var sourceModelBuilder = new BasicModelBuilder();
            sourceModelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.Index("Id").IsUnique().ForRelational().Name("IX");
                    });

            var targetModelBuilder = new BasicModelBuilder();
            targetModelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Key("Id");
                    // TODO: SQLite-specific. Issue #875
                    b.Index("Id").IsUnique().ForRelational().Name("IX2");
                });

            var renameIndexOperation = new RenameIndexOperation("T", "IX", "IX2");

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(renameIndexOperation);

            var operations = PreProcess(sourceModelBuilder.Model, targetModelBuilder.Model, operationCollection);

            Assert.Equal(2, operations.Count);
            Assert.IsType<DropIndexOperation>(operations[0]);
            Assert.IsType<CreateIndexOperation>(operations[1]);

            var dropIndexOperation = (DropIndexOperation)operations[0];

            Assert.Equal("T", dropIndexOperation.TableName);
            Assert.Equal("IX", dropIndexOperation.IndexName);

            var createIndexOperation = (CreateIndexOperation)operations[1];

            Assert.Equal("T", createIndexOperation.TableName);
            Assert.Equal("IX", createIndexOperation.IndexName);
            Assert.Equal(new[] { "Id" }, createIndexOperation.ColumnNames);
            Assert.True(createIndexOperation.IsUnique);
        }

        private static IReadOnlyList<MigrationOperation> PreProcess(IModel sourceModel, IModel targetModel, MigrationOperationCollection operations)
        {
            return CreatePreProcessor().Process(operations, sourceModel, targetModel);
        }

        private static MigrationOperationFactory OperationFactory()
        {
            var extensionProvider = new RelationalMetadataExtensionProvider();
            var nameGenerator = new RelationalNameGenerator(extensionProvider);

            return new MigrationOperationFactory(extensionProvider, nameGenerator);
        }

        private static SQLiteMigrationOperationPreProcessor CreatePreProcessor()
        {
            var extensionProvider = new RelationalMetadataExtensionProvider();
            var nameGenerator = new RelationalNameGenerator(extensionProvider);
            var typeMapper = new SQLiteTypeMapper();
            var operationFactory = new MigrationOperationFactory(extensionProvider, nameGenerator);

            return new SQLiteMigrationOperationPreProcessor(extensionProvider, nameGenerator, typeMapper, operationFactory);
        }
    }
}
