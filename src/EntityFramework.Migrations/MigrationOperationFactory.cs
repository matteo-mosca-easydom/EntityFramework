// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Migrations.Utilities;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;

namespace Microsoft.Data.Entity.Migrations
{
    public class MigrationOperationFactory
    {
        private readonly IRelationalMetadataExtensionProvider _extensionProvider;
        private readonly RelationalNameGenerator _nameGenerator;

        public MigrationOperationFactory(
            [NotNull] IRelationalMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalNameGenerator nameGenerator)
        {
            Check.NotNull(extensionProvider, "extensionProvider");
            Check.NotNull(nameGenerator, "nameGenerator");

            _extensionProvider = extensionProvider;
            _nameGenerator = nameGenerator;
        }

        public virtual IRelationalMetadataExtensionProvider ExtensionProvider
        {
            get { return _extensionProvider; }
        }

        public virtual RelationalNameGenerator NameGenerator
        {
            get { return _nameGenerator; }
        }

        public virtual DropSequenceOperation DropSequenceOperation([NotNull] ISequence source)
        {
            Check.NotNull(source, "source");

            return new DropSequenceOperation(NameGenerator.FullSequenceName(source));
        }

        public virtual MoveSequenceOperation MoveSequenceOperation(SchemaQualifiedName sequenceName, [NotNull] string targetSchema)
        {
            Check.NotEmpty(targetSchema, "targetSchema");

            return new MoveSequenceOperation(sequenceName, targetSchema);
        }

        public virtual RenameSequenceOperation RenameSequenceOperation(SchemaQualifiedName sequenceName, [NotNull] string targetName)
        {
            Check.NotEmpty(targetName, "targetName");

            return new RenameSequenceOperation(sequenceName, targetName);
        }

        public virtual CreateSequenceOperation CreateSequenceOperation([NotNull] ISequence target)
        {
            Check.NotNull(target, "target");

            return 
                new CreateSequenceOperation(
                    NameGenerator.FullSequenceName(target),
                    target.StartValue, 
                    target.IncrementBy, 
                    target.MinValue, 
                    target.MaxValue, 
                    target.Type);
        }

        public virtual DropTableOperation DropTableOperation([NotNull] IEntityType source)
        {
            Check.NotNull(source, "source");

            return new DropTableOperation(NameGenerator.FullTableName(source));
        }

        public virtual MoveTableOperation MoveTableOperation(SchemaQualifiedName tableName, [NotNull] string targetSchema)
        {
            Check.NotEmpty(targetSchema, "targetSchema");

            return new MoveTableOperation(tableName, targetSchema);
        }

        public virtual RenameTableOperation RenameTableOperation(SchemaQualifiedName tableName, [NotNull] string targetName)
        {
            Check.NotEmpty(targetName, "targetName");

            return new RenameTableOperation(tableName, targetName);
        }

        public virtual CreateTableOperation CreateTableOperation([NotNull] IEntityType target)
        {
            Check.NotNull(target, "target");

            var operation = new CreateTableOperation(NameGenerator.FullTableName(target));

            operation.Columns.AddRange(target.Properties.Select(Column));

            var primaryKey = target.GetPrimaryKey();
            if (primaryKey != null)
            {
                operation.PrimaryKey = AddPrimaryKeyOperation(primaryKey);
            }

            operation.UniqueConstraints.AddRange(target.Keys.Where(key => key != primaryKey).Select(AddUniqueConstraintOperation));
            operation.ForeignKeys.AddRange(target.ForeignKeys.Select(AddForeignKeyOperation));
            operation.Indexes.AddRange(target.Indexes.Select(CreateIndexOperation));

            return operation;
        }

        public virtual DropColumnOperation DropColumnOperation([NotNull] IProperty source)
        {
            Check.NotNull(source, "source");

            return new DropColumnOperation(NameGenerator.FullTableName(source.EntityType), NameGenerator.ColumnName(source));
        }

        public virtual RenameColumnOperation RenameColumnOperation(SchemaQualifiedName tableName, string sourceName, string targetName)
        {
            Check.NotEmpty(sourceName, "sourceName");
            Check.NotEmpty(targetName, "targetName");

            return new RenameColumnOperation(tableName, sourceName, tableName);
        }

        public virtual AlterColumnOperation AlterColumnOperation([NotNull] IProperty target, bool isDestructiveChange)
        {
            Check.NotNull(target, "target");

            return 
                new AlterColumnOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    Column(target), 
                    isDestructiveChange);
        }

        public virtual AddColumnOperation AddColumnOperation([NotNull] IProperty target)
        {
            Check.NotNull(target, "target");

            return 
                new AddColumnOperation(
                    NameGenerator.FullTableName(target.EntityType), 
                    Column(target));
        }

        public virtual DropDefaultConstraintOperation DropDefaultConstraintOperation([NotNull] IProperty source)
        {
            Check.NotNull(source, "source");

            return 
                new DropDefaultConstraintOperation(
                    NameGenerator.FullTableName(source.EntityType), 
                    NameGenerator.ColumnName(source));
        }

        public virtual AddDefaultConstraintOperation AddDefaultConstraintOperation([NotNull] IProperty target)
        {
            Check.NotNull(target, "target");

            var extensions = ExtensionProvider.Extensions(target);

            return 
                new AddDefaultConstraintOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    NameGenerator.ColumnName(target), 
                    extensions.DefaultValue, 
                    extensions.DefaultExpression);
        }

        public virtual DropPrimaryKeyOperation DropPrimaryKeyOperation([NotNull] IKey source)
        {
            Check.NotNull(source, "source");

            return 
                new DropPrimaryKeyOperation(
                    NameGenerator.FullTableName(source.EntityType), 
                    NameGenerator.KeyName(source));
        }

        public virtual AddPrimaryKeyOperation AddPrimaryKeyOperation([NotNull] IKey target)
        {
            Check.NotNull(target, "target");

            return 
                new AddPrimaryKeyOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    NameGenerator.KeyName(target),
                    target.Properties.Select(p => NameGenerator.ColumnName(p)).ToList(),
                    // TODO: Issue #879: Clustered is SQL Server-specific.
                    isClustered: true);
        }

        public virtual DropUniqueConstraintOperation DropUniqueConstraintOperation([NotNull] IKey source)
        {
            Check.NotNull(source, "source");

            return
                new DropUniqueConstraintOperation(
                    NameGenerator.FullTableName(source.EntityType),
                    NameGenerator.KeyName(source));
        }

        public virtual AddUniqueConstraintOperation AddUniqueConstraintOperation([NotNull] IKey target)
        {
            Check.NotNull(target, "target");

            return
                new AddUniqueConstraintOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    NameGenerator.KeyName(target),
                    target.Properties.Select(p => NameGenerator.ColumnName(p)).ToList());
        }

        public virtual DropForeignKeyOperation DropForeignKeyOperation([NotNull] IForeignKey source)
        {
            Check.NotNull(source, "source");

            return
                new DropForeignKeyOperation(
                    NameGenerator.FullTableName(source.EntityType),
                    NameGenerator.ForeignKeyName(source));
        }

        public virtual AddForeignKeyOperation AddForeignKeyOperation([NotNull] IForeignKey target)
        {
            Check.NotNull(target, "target");

            return
                new AddForeignKeyOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    NameGenerator.ForeignKeyName(target),
                    target.Properties.Select(p => NameGenerator.ColumnName(p)).ToList(),
                    NameGenerator.FullTableName(target.ReferencedEntityType),
                    target.ReferencedProperties.Select(p => NameGenerator.ColumnName(p)).ToList(),
                    // TODO: Issue #333: Cascading behaviors not supported.
                    cascadeDelete: false);
        }

        public virtual DropIndexOperation DropIndexOperation([NotNull] IIndex source)
        {
            Check.NotNull(source, "source");

            return
                new DropIndexOperation(
                    NameGenerator.FullTableName(source.EntityType),
                    NameGenerator.IndexName(source));
        }

        public virtual RenameIndexOperation RenameIndexOperation(SchemaQualifiedName tableName, [NotNull] string sourceName, [NotNull] string targetName)
        {
            Check.NotEmpty(sourceName, "sourceName");
            Check.NotEmpty(targetName, "targetName");

            return new RenameIndexOperation(tableName, sourceName, targetName);
        }

        public virtual CreateIndexOperation CreateIndexOperation([NotNull] IIndex target)
        {
            Check.NotNull(target, "target");

            return
                new CreateIndexOperation(
                    NameGenerator.FullTableName(target.EntityType),
                    NameGenerator.IndexName(target),
                    target.Properties.Select(p => NameGenerator.ColumnName(p)).ToList(),
                    target.IsUnique,
                    // TODO: Issue #879: Clustered is SQL Server-specific.
                    isClustered: false);
        }

        public virtual Column Column([NotNull] IProperty property)
        {
            Check.NotNull(property, "property");

            var extensions = ExtensionProvider.Extensions(property);

            return
                new Column(NameGenerator.ColumnName(property), property.PropertyType, extensions.ColumnType)
                    {
                        IsNullable = property.IsNullable,
                        DefaultValue = extensions.DefaultValue,
                        DefaultSql = extensions.DefaultExpression,
                        GenerateValueOnAdd = property.GenerateValueOnAdd,
                        IsComputed = property.IsStoreComputed,
                        IsTimestamp = property.IsConcurrencyToken && property.PropertyType == typeof(byte[]),
                        MaxLength = property.MaxLength > 0 ? property.MaxLength : (int?)null
                    };
        }
    }
}
