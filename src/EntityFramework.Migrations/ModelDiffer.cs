// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    public class ModelDiffer
    {
        private MigrationOperationCollection _operations;

        private IModel _sourceModel;
        private IModel _targetModel;

        private readonly IRelationalMetadataExtensionProvider _extensionProvider;
        private readonly RelationalNameGenerator _nameGenerator;
        private readonly RelationalTypeMapper _typeMapper;
        private readonly MigrationOperationFactory _operationFactory;
        private readonly MigrationOperationProcessor _operationProcessor;

        public ModelDiffer(
            [NotNull] IRelationalMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalNameGenerator nameGenerator,
            [NotNull] RelationalTypeMapper typeMapper,
            [NotNull] MigrationOperationFactory operationFactory,
            [NotNull] MigrationOperationProcessor operationProcessor)
        {
            Check.NotNull(extensionProvider, "extensionProvider");
            Check.NotNull(nameGenerator, "nameGenerator");
            Check.NotNull(typeMapper, "typeMapper");
            Check.NotNull(operationFactory, "operationFactory");
            Check.NotNull(operationProcessor, "operationProcessor");

            _extensionProvider = extensionProvider;
            _nameGenerator = nameGenerator;
            _typeMapper = typeMapper;
            _operationFactory = operationFactory;
            _operationProcessor = operationProcessor;
        }

        public virtual IRelationalMetadataExtensionProvider ExtensionProvider
        {
            get { return _extensionProvider; }
        }

        public virtual RelationalNameGenerator NameGenerator
        {
            get { return _nameGenerator; }
        }

        public virtual RelationalTypeMapper TypeMapper
        {
            get { return _typeMapper; }
        }

        public virtual MigrationOperationFactory OperationFactory
        {
            get { return _operationFactory; }
        }

        public virtual MigrationOperationProcessor OperationProcessor
        {
            get { return _operationProcessor; }
        }

        public virtual IReadOnlyList<MigrationOperation> CreateSchema([NotNull] IModel model)
        {
            Check.NotNull(model, "model");

            var createSequenceOperations = GetSequences(model)
                .Select(s => OperationFactory.CreateSequenceOperation(s));

            var createTableOperations = model.EntityTypes
                .Select(t => OperationFactory.CreateTableOperation(t));

            var addForeignKeyOperations = model.EntityTypes
                .SelectMany(t => t.ForeignKeys)
                .Select(fk => OperationFactory.AddForeignKeyOperation(fk));

            var createIndexOperations = model.EntityTypes
                .SelectMany(t => t.Indexes)
                .Select(ix => OperationFactory.CreateIndexOperation(ix));

            return
                ((IEnumerable<MigrationOperation>)createSequenceOperations)
                    .Concat(createTableOperations)
                    .Concat(addForeignKeyOperations)
                    .Concat(createIndexOperations)
                    .ToList();
        }

        public virtual IReadOnlyList<MigrationOperation> DropSchema([NotNull] IModel model)
        {
            Check.NotNull(model, "model");

            var dropSequenceOperations = GetSequences(model).Select(
                s => OperationFactory.DropSequenceOperation(s));

            var dropTableOperations = model.EntityTypes.Select(
                t => OperationFactory.DropTableOperation(t));

            return
                ((IEnumerable<MigrationOperation>)dropSequenceOperations)
                    .Concat(dropTableOperations)
                    .ToList();
        }

        public virtual IReadOnlyList<MigrationOperation> Diff([NotNull] IModel source, [NotNull] IModel target)
        {
            Check.NotNull(source, "source");
            Check.NotNull(target, "target");

            _sourceModel = source;
            _targetModel = target;
            _operations = new MigrationOperationCollection();

            DiffSequences(source, target);
            DiffTables(source, target);            

            // TODO: Add more unit tests for the operation order.

            HandleTransitiveRenames();

            return OperationProcessor.Process(_operations, _sourceModel, _targetModel);
        }

        private void DiffTables(IModel source, IModel target)
        {
            var tablePairs = FindEntityTypePairs();
            var columnPairs = new IReadOnlyList<Tuple<IProperty, IProperty>>[tablePairs.Count];
            var columnMap = new Dictionary<IProperty, IProperty>();

            for (var i = 0; i < tablePairs.Count; i++)
            {
                var tableColumnPairs = FindColumnPairs(tablePairs[i]);

                columnPairs[i] = tableColumnPairs;

                foreach (var pair in tableColumnPairs)
                {
                    columnMap.Add(pair.Item1, pair.Item2);
                }
            }

            FindMovedTables(tablePairs);
            FindRenamedTables(tablePairs);
            FindCreatedTables(tablePairs);
            FindDroppedTables(tablePairs);

            for (var i = 0; i < tablePairs.Count; i++)
            {
                var tablePair = tablePairs[i];
                var tableColumnPairs = columnPairs[i];

                FindRenamedColumns(tableColumnPairs);
                FindAddedColumns(tablePair, tableColumnPairs);
                FindDroppedColumns(tablePair, tableColumnPairs);
                FindAlteredColumns(tableColumnPairs);

                FindPrimaryKeyChanges(tablePair, columnMap);

                var uniqueConstraintPairs = FindUniqueConstraintPairs(tablePair, columnMap);

                FindAddedUniqueConstraints(tablePair, uniqueConstraintPairs);
                FindDroppedUniqueConstraints(tablePair, uniqueConstraintPairs);

                var foreignKeyPairs = FindForeignKeyPairs(tablePair, columnMap);

                FindAddedForeignKeys(tablePair, foreignKeyPairs);
                FindDroppedForeignKeys(tablePair, foreignKeyPairs);

                var indexPairs = FindIndexPairs(tablePair, columnMap);

                FindRenamedIndexes(indexPairs);
                FindCreatedIndexes(tablePair, indexPairs);
                FindDroppedIndexes(tablePair, indexPairs);
            }
        }

        private void DiffSequences(IModel source, IModel target)
        {
            var sourceSequences = GetSequences(source);
            var targetSequences = GetSequences(target);

            var sequencePairs = FindSequencePairs(sourceSequences, targetSequences);

            FindMovedSequences(sequencePairs);
            FindRenamedSequences(sequencePairs);
            FindCreatedSequences(sequencePairs, targetSequences);
            FindDroppedSequences(sequencePairs, sourceSequences);
            FindAlteredSequences(sequencePairs);
        }

        private void HandleTransitiveRenames()
        {
            const string temporaryNamePrefix = "__mig_tmp__";
            var temporaryNameIndex = 0;

            _operations.Set(HandleTransitiveRenames(
                _operations.Get<RenameSequenceOperation>(),
                op => null,
                op => op.SequenceName,
                op => new SchemaQualifiedName(op.NewSequenceName, op.SequenceName.Schema),
                op => new SchemaQualifiedName(temporaryNamePrefix + temporaryNameIndex++, op.SequenceName.Schema),
                (parentName, name, newName) => new RenameSequenceOperation(name, SchemaQualifiedName.Parse(newName).Name)));

            _operations.Set(HandleTransitiveRenames(
                _operations.Get<RenameTableOperation>(),
                op => null,
                op => op.TableName,
                op => new SchemaQualifiedName(op.NewTableName, op.TableName.Schema),
                op => new SchemaQualifiedName(temporaryNamePrefix + temporaryNameIndex++, op.TableName.Schema),
                (parentName, name, newName) => new RenameTableOperation(name, SchemaQualifiedName.Parse(newName).Name)));

            _operations.Set(HandleTransitiveRenames(
                _operations.Get<RenameColumnOperation>(),
                op => op.TableName,
                op => op.ColumnName,
                op => op.NewColumnName,
                op => temporaryNamePrefix + temporaryNameIndex++,
                (parentName, name, newName) => new RenameColumnOperation(parentName, name, newName)));

            _operations.Set(HandleTransitiveRenames(
                _operations.Get<RenameIndexOperation>(),
                op => op.TableName,
                op => op.IndexName,
                op => op.NewIndexName,
                op => temporaryNamePrefix + temporaryNameIndex++,
                (parentName, name, newName) => new RenameIndexOperation(parentName, name, newName)));
        }

        private static IEnumerable<T> HandleTransitiveRenames<T>(
            IReadOnlyList<T> renameOperations,
            Func<T, string> getParentName,
            Func<T, string> getName,
            Func<T, string> getNewName,
            Func<T, string> generateTempName,
            Func<string, string, string, T> createRenameOperation)
            where T : MigrationOperation
        {
            var tempRenameOperations = new List<T>();

            for (var i = 0; i < renameOperations.Count; i++)
            {
                var renameOperation = renameOperations[i];

                var dependentRenameOperation
                    = renameOperations
                        .Skip(i + 1)
                        .SingleOrDefault(r => getName(r) == getNewName(renameOperation));

                if (dependentRenameOperation != null)
                {
                    var tempName = generateTempName(renameOperation);

                    tempRenameOperations.Add(
                        createRenameOperation(
                            getParentName(renameOperation),
                            tempName,
                            getNewName(renameOperation)));

                    renameOperation
                        = createRenameOperation(
                            getParentName(renameOperation),
                            getName(renameOperation),
                            tempName);
                }

                yield return renameOperation;
            }

            foreach (var renameOperation in tempRenameOperations)
            {
                yield return renameOperation;
            }
        }

        private IReadOnlyList<Tuple<IEntityType, IEntityType>> FindEntityTypePairs()
        {
            var simpleMatchPairs =
                (from et1 in _sourceModel.EntityTypes
                    from et2 in _targetModel.EntityTypes
                    where SimpleMatchEntityTypes(et1, et2)
                    select Tuple.Create(et1, et2))
                    .ToArray();

            var fuzzyMatchPairs =
                from et1 in _sourceModel.EntityTypes.Except(simpleMatchPairs.Select(p => p.Item1))
                from et2 in _targetModel.EntityTypes.Except(simpleMatchPairs.Select(p => p.Item2))
                where FuzzyMatchEntityTypes(et1, et2)
                select Tuple.Create(et1, et2);

            return simpleMatchPairs.Concat(fuzzyMatchPairs).ToArray();
        }

        private void FindMovedTables(
            IEnumerable<Tuple<IEntityType, IEntityType>> tablePairs)
        {
            _operations.AddRange(
                tablePairs
                    .Where(pair => !MatchTableSchemas(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.MoveTableOperation(
                        NameGenerator.FullTableName(pair.Item1),
                        NameGenerator.TableSchema(pair.Item2))));
        }

        private void FindRenamedTables(
            IEnumerable<Tuple<IEntityType, IEntityType>> tablePairs)
        {
            _operations.AddRange(
                tablePairs
                    .Where(pair => !MatchTableNames(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.RenameTableOperation(
                        new SchemaQualifiedName(
                            NameGenerator.TableName(pair.Item1),
                            NameGenerator.TableSchema(pair.Item2)),
                        NameGenerator.TableName(pair.Item2))));
        }

        private void FindCreatedTables(
            IEnumerable<Tuple<IEntityType, IEntityType>> tablePairs)
        {
            var tables =
                _targetModel.EntityTypes
                    .Except(tablePairs.Select(p => p.Item2))
                    .ToArray();

            _operations.AddRange(
                tables.Select(t => OperationFactory.CreateTableOperation(t)));

            _operations.AddRange(
                tables
                    .SelectMany(t => t.ForeignKeys)
                    .Select(fk => OperationFactory.AddForeignKeyOperation(fk)));

            _operations.AddRange(
                tables
                    .SelectMany(t => t.Indexes)
                    .Select(ix => OperationFactory.CreateIndexOperation(ix)));
        }

        private void FindDroppedTables(
            IEnumerable<Tuple<IEntityType, IEntityType>> tablePairs)
        {
            _operations.AddRange(
                _sourceModel.EntityTypes
                    .Except(tablePairs.Select(p => p.Item1))
                    .Select(t => OperationFactory.DropTableOperation(t)));
        }

        private IReadOnlyList<Tuple<IProperty, IProperty>> FindColumnPairs(
            Tuple<IEntityType, IEntityType> tablePair)
        {
            var simplePropertyMatchPairs =
                (from p1 in tablePair.Item1.Properties
                    from p2 in tablePair.Item2.Properties
                    where SimpleMatchProperties(p1, p2)
                    select Tuple.Create(p1, p2))
                    .ToList();

            var simpleColumnMatchPairs =
                from p1 in tablePair.Item1.Properties.Except(simplePropertyMatchPairs.Select(p => p.Item1))
                from p2 in tablePair.Item2.Properties.Except(simplePropertyMatchPairs.Select(p => p.Item2))
                where MatchColumnNames(p1, p2)
                select Tuple.Create(p1, p2);

            return simplePropertyMatchPairs.Concat(simpleColumnMatchPairs).ToArray();
        }

        private void FindRenamedColumns(
            IEnumerable<Tuple<IProperty, IProperty>> columnPairs)
        {
            _operations.AddRange(
                columnPairs
                    .Where(pair => !MatchColumnNames(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.RenameColumnOperation(
                        NameGenerator.FullTableName(pair.Item2.EntityType),
                        NameGenerator.ColumnName(pair.Item1), 
                        NameGenerator.ColumnName(pair.Item2))));
        }

        private void FindAddedColumns(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IProperty, IProperty>> columnPairs)
        {
            _operations.AddRange(
                tablePair.Item2.Properties
                    .Except(columnPairs.Select(pair => pair.Item2))
                    .Select(c => OperationFactory.AddColumnOperation(c)));
        }

        private void FindDroppedColumns(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IProperty, IProperty>> columnPairs)
        {
            _operations.AddRange(
                tablePair.Item1.Properties
                    .Except(columnPairs.Select(pair => pair.Item1))
                    .Select(c => OperationFactory.DropColumnOperation(c)));
        }

        private void FindAlteredColumns(
            IEnumerable<Tuple<IProperty, IProperty>> columnPairs)
        {
            _operations.AddRange(
                columnPairs.Where(pair => !EquivalentColumns(pair.Item1, pair.Item2))
                .Select(pair => OperationFactory.AlterColumnOperation(pair.Item2, isDestructiveChange : true)));

            // TODO: Add functionality to determine the value of isDestructiveChange.
        }

        private void FindPrimaryKeyChanges(
            Tuple<IEntityType, IEntityType> tablePair,
            IDictionary<IProperty, IProperty> columnMap)
        {
            var sourcePrimaryKey = tablePair.Item1.GetPrimaryKey();
            var targetPrimaryKey = tablePair.Item2.GetPrimaryKey();

            if (targetPrimaryKey == null)
            {
                if (sourcePrimaryKey == null)
                {
                    return;
                }

                DropPrimaryKey(sourcePrimaryKey);
            }
            else if (sourcePrimaryKey == null)
            {
                AddPrimaryKey(targetPrimaryKey);
            }
            else if (!EquivalentPrimaryKeys(sourcePrimaryKey, targetPrimaryKey, columnMap))
            {
                DropPrimaryKey(sourcePrimaryKey);
                AddPrimaryKey(targetPrimaryKey);
            }
        }

        private void AddPrimaryKey(IKey key)
        {
            _operations.Add(OperationFactory.AddPrimaryKeyOperation(key));
        }

        private void DropPrimaryKey(IKey key)
        {
            _operations.Add(OperationFactory.DropPrimaryKeyOperation(key));
        }

        private IReadOnlyList<Tuple<IKey, IKey>> FindUniqueConstraintPairs(
            Tuple<IEntityType, IEntityType> table,
            IDictionary<IProperty, IProperty> columnMap)
        {
            var pk1 = table.Item1.GetPrimaryKey();
            var pk2 = table.Item2.GetPrimaryKey();

            return
                (from uc1 in table.Item1.Keys.Where(k => k != pk1)
                    from uc2 in table.Item2.Keys.Where(k => k != pk2)
                    where EquivalentUniqueConstraints(uc1, uc2, columnMap)
                    select Tuple.Create(uc1, uc2))
                    .ToArray();
        }

        private void FindAddedUniqueConstraints(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IKey, IKey>> uniqueConstraintPairs)
        {
            var pk2 = tablePair.Item2.GetPrimaryKey();

            _operations.AddRange(
                tablePair.Item2.Keys.Where(k => k != pk2)
                    .Except(uniqueConstraintPairs.Select(pair => pair.Item2))
                    .Select(uc => OperationFactory.AddUniqueConstraintOperation(uc)));
        }

        private void FindDroppedUniqueConstraints(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IKey, IKey>> uniqueConstraintPairs)
        {
            var pk1 = tablePair.Item1.GetPrimaryKey();

            _operations.AddRange(
                tablePair.Item1.Keys.Where(k => k != pk1)
                    .Except(uniqueConstraintPairs.Select(pair => pair.Item1))
                    .Select(uc => OperationFactory.DropUniqueConstraintOperation(uc)));
        }

        private IReadOnlyList<Tuple<IForeignKey, IForeignKey>> FindForeignKeyPairs(
            Tuple<IEntityType, IEntityType> table,
            IDictionary<IProperty, IProperty> columnMap)
        {
            return
                (from fk1 in table.Item1.ForeignKeys
                    from fk2 in table.Item2.ForeignKeys
                    where EquivalentForeignKeys(fk1, fk2, columnMap)
                    select Tuple.Create(fk1, fk2))
                    .ToArray();
        }

        private void FindAddedForeignKeys(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IForeignKey, IForeignKey>> foreignKeyPairs)
        {
            _operations.AddRange(
                tablePair.Item2.ForeignKeys
                    .Except(foreignKeyPairs.Select(pair => pair.Item2))
                    .Select(fk => OperationFactory.AddForeignKeyOperation(fk)));
        }

        private void FindDroppedForeignKeys(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IForeignKey, IForeignKey>> foreignKeyPairs)
        {
            _operations.AddRange(
                tablePair.Item1.ForeignKeys
                    .Except(foreignKeyPairs.Select(pair => pair.Item1))
                    .Select(fk => OperationFactory.DropForeignKeyOperation(fk)));
        }

        private IReadOnlyList<Tuple<IIndex, IIndex>> FindIndexPairs(
            Tuple<IEntityType, IEntityType> tablePair,
            IDictionary<IProperty, IProperty> columnMap)
        {
            return
                (from ix1 in tablePair.Item1.Indexes
                    from ix2 in tablePair.Item2.Indexes
                    where EquivalentIndexes(ix1, ix2, columnMap)
                    select Tuple.Create(ix1, ix2))
                    .ToArray();
        }

        private void FindRenamedIndexes(
            IEnumerable<Tuple<IIndex, IIndex>> indexPairs)
        {
            _operations.AddRange(
                indexPairs
                    .Where(pair => !MatchIndexNames(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.RenameIndexOperation(
                        NameGenerator.FullTableName(pair.Item2.EntityType),
                        NameGenerator.IndexName(pair.Item1),
                        NameGenerator.IndexName(pair.Item2))));
        }

        private void FindCreatedIndexes(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IIndex, IIndex>> indexPairs)
        {
            _operations.AddRange(
                tablePair.Item2.Indexes
                    .Except(indexPairs.Select(pair => pair.Item2))
                    .Select(ix => OperationFactory.CreateIndexOperation(ix)));
        }

        private void FindDroppedIndexes(
            Tuple<IEntityType, IEntityType> tablePair,
            IEnumerable<Tuple<IIndex, IIndex>> indexPairs)
        {
            _operations.AddRange(
                tablePair.Item1.Indexes
                    .Except(indexPairs.Select(pair => pair.Item1))
                    .Select(ix => OperationFactory.DropIndexOperation(ix)));
        }

        private IReadOnlyList<Tuple<ISequence, ISequence>> FindSequencePairs(
            [NotNull] IReadOnlyList<ISequence> sourceSequences,
            [NotNull] IReadOnlyList<ISequence> targetSequences)
        {
            return
                (from source in sourceSequences
                    from target in targetSequences
                    where MatchSequenceNames(source, target) && MatchSequenceSchemas(source, target)
                    select Tuple.Create(source, target))
                    .ToList();
        }

        private void FindMovedSequences(IEnumerable<Tuple<ISequence, ISequence>> sequencePairs)
        {
            _operations.AddRange(
                sequencePairs
                    .Where(pair => !MatchSequenceSchemas(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.MoveSequenceOperation(
                        NameGenerator.FullSequenceName(pair.Item1),
                        NameGenerator.SequenceSchema(pair.Item2))));
        }

        private void FindRenamedSequences(IEnumerable<Tuple<ISequence, ISequence>> sequencePairs)
        {
            _operations.AddRange(
                sequencePairs
                    .Where(pair => !MatchSequenceNames(pair.Item1, pair.Item2))
                    .Select(pair => OperationFactory.RenameSequenceOperation(
                        new SchemaQualifiedName(
                            NameGenerator.SequenceName(pair.Item1),
                            NameGenerator.SequenceSchema(pair.Item2)),
                        NameGenerator.SequenceName(pair.Item2))));
        }

        private void FindCreatedSequences(IEnumerable<Tuple<ISequence, ISequence>> sequencePairs, IEnumerable<ISequence> targetSequences)
        {
            _operations.AddRange(
                targetSequences
                    .Except(sequencePairs.Select(p => p.Item2))
                    .Select(s => OperationFactory.CreateSequenceOperation(s)));
        }

        private void FindDroppedSequences(IEnumerable<Tuple<ISequence, ISequence>> sequencePairs, IEnumerable<ISequence> sourceSequences)
        {
            _operations.AddRange(
                sourceSequences
                    .Except(sequencePairs.Select(p => p.Item1))
                    .Select(s => OperationFactory.DropSequenceOperation(s)));
        }

        private void FindAlteredSequences(IEnumerable<Tuple<ISequence, ISequence>> sequencePairs)
        {
            _operations.AddRange(
                sequencePairs
                    .Where(pair => !EquivalentSequences(pair.Item1, pair.Item2))
                    .Select(pair =>
                        new AlterSequenceOperation(
                            pair.Item2.Name,
                            pair.Item2.IncrementBy)));
        }

        protected virtual bool SimpleMatchEntityTypes([NotNull] IEntityType sourceEntityType, [NotNull] IEntityType targetEntityType)
        {
            Check.NotNull(sourceEntityType, "sourceEntityType");
            Check.NotNull(targetEntityType, "targetEntityType");

            return sourceEntityType.Name == targetEntityType.Name;
        }

        protected virtual bool FuzzyMatchEntityTypes([NotNull] IEntityType sourceEntityType, [NotNull] IEntityType targetEntityType)
        {
            Check.NotNull(sourceEntityType, "sourceEntityType");
            Check.NotNull(targetEntityType, "targetEntityType");

            var matchingPropertyCount =
                (from p1 in sourceEntityType.Properties
                    from p2 in targetEntityType.Properties
                    where EquivalentProperties(p1, p2)
                    select 1)
                    .Count();

            // At least 80% of properties, across both entities, must be equivalent.
            return (matchingPropertyCount * 2.0f / (sourceEntityType.Properties.Count + targetEntityType.Properties.Count)) >= 0.80;
        }

        protected virtual bool EquivalentProperties([NotNull] IProperty sourceProperty, [NotNull] IProperty targetProperty)
        {
            Check.NotNull(sourceProperty, "sourceProperty");
            Check.NotNull(targetProperty, "targetProperty");

            return
                sourceProperty.Name == targetProperty.Name
                && sourceProperty.PropertyType == targetProperty.PropertyType;
        }

        protected virtual bool SimpleMatchProperties([NotNull] IProperty sourceProperty, [NotNull] IProperty targetProperty)
        {
            Check.NotNull(sourceProperty, "sourceProperty");
            Check.NotNull(targetProperty, "targetProperty");

            return sourceProperty.Name == targetProperty.Name;
        }

        protected virtual bool MatchSequenceNames(ISequence source, ISequence target)
        {
            return source.Name == target.Name;
        }

        protected virtual bool MatchSequenceSchemas(ISequence source, ISequence target)
        {
            return source.Schema == target.Schema;
        }

        protected virtual bool MatchTableNames(IEntityType source, IEntityType target)
        {
            return ExtensionProvider.Extensions(source).Table == ExtensionProvider.Extensions(target).Table;
        }

        protected virtual bool MatchTableSchemas(IEntityType source, IEntityType target)
        {
            return ExtensionProvider.Extensions(source).Schema == ExtensionProvider.Extensions(target).Schema;
        }

        protected virtual bool MatchColumnNames(IProperty source, IProperty target)
        {
            return ExtensionProvider.Extensions(source).Column == ExtensionProvider.Extensions(target).Column;
        }

        protected virtual bool MatchIndexNames(IIndex source, IIndex target)
        {
            return ExtensionProvider.Extensions(source).Name == ExtensionProvider.Extensions(target).Name;
        }

        protected virtual bool EquivalentColumns([NotNull] IProperty source, [NotNull] IProperty target)
        {
            Check.NotNull(source, "source");
            Check.NotNull(target, "target");

            var sourceExtensions = ExtensionProvider.Extensions(source);
            var targetExtensions = ExtensionProvider.Extensions(target);

            return
                source.PropertyType == target.PropertyType
                && ColumnType(source) == ColumnType(target)
                && sourceExtensions.DefaultValue == targetExtensions.DefaultValue
                && sourceExtensions.DefaultExpression == targetExtensions.DefaultExpression
                && source.IsNullable == target.IsNullable
                && source.GenerateValueOnAdd == target.GenerateValueOnAdd
                && source.IsStoreComputed == target.IsStoreComputed
                && source.IsConcurrencyToken == target.IsConcurrencyToken
                && source.MaxLength == target.MaxLength;
        }

        protected virtual bool EquivalentSequences([NotNull] ISequence source, [NotNull] ISequence target)
        {
            Check.NotNull(source, "source");
            Check.NotNull(target, "target");

            return
                source.IncrementBy == target.IncrementBy;
        }

        protected virtual bool EquivalentPrimaryKeys(
            [NotNull] IKey sourceKey,
            [NotNull] IKey targetKey,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceKey, "sourceKey");
            Check.NotNull(targetKey, "targetKey");
            Check.NotNull(columnMap, "columnMap");

            return
                NameGenerator.KeyName(sourceKey) == NameGenerator.KeyName(targetKey)
                && EquivalentColumnReferences(sourceKey.Properties, targetKey.Properties, columnMap);
        }

        protected virtual bool EquivalentUniqueConstraints(
            [NotNull] IKey sourceKey,
            [NotNull] IKey targetKey,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceKey, "sourceKey");
            Check.NotNull(targetKey, "targetKey");
            Check.NotNull(columnMap, "columnMap");

            return
                NameGenerator.KeyName(sourceKey) == NameGenerator.KeyName(targetKey)
                && EquivalentColumnReferences(sourceKey.Properties, targetKey.Properties, columnMap);
        }

        protected virtual bool EquivalentForeignKeys(
            [NotNull] IForeignKey sourceForeignKey,
            [NotNull] IForeignKey targetForeignKey,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceForeignKey, "sourceForeignKey");
            Check.NotNull(targetForeignKey, "targetForeignKey");
            Check.NotNull(columnMap, "columnMap");

            return
                NameGenerator.ForeignKeyName(sourceForeignKey) == NameGenerator.ForeignKeyName(targetForeignKey)
                && EquivalentColumnReferences(sourceForeignKey.Properties, targetForeignKey.Properties, columnMap)
                && EquivalentColumnReferences(sourceForeignKey.ReferencedProperties, targetForeignKey.ReferencedProperties, columnMap);
        }

        protected virtual bool EquivalentIndexes(
            [NotNull] IIndex sourceIndex,
            [NotNull] IIndex targetIndex,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceIndex, "sourceIndex");
            Check.NotNull(targetIndex, "targetIndex");
            Check.NotNull(columnMap, "columnMap");

            return
                sourceIndex.IsUnique == targetIndex.IsUnique
                && EquivalentColumnReferences(sourceIndex.Properties, targetIndex.Properties, columnMap);
        }

        protected virtual bool EquivalentColumnReferences(
            [NotNull] IProperty sourceColumn,
            [NotNull] IProperty targetColumn,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceColumn, "sourceColumn");
            Check.NotNull(targetColumn, "targetColumn");
            Check.NotNull(columnMap, "columnMap");

            IProperty column;
            return columnMap.TryGetValue(sourceColumn, out column) && ReferenceEquals(column, targetColumn);
        }

        protected virtual bool EquivalentColumnReferences(
            [NotNull] IReadOnlyList<IProperty> sourceColumns,
            [NotNull] IReadOnlyList<IProperty> targetColumns,
            [NotNull] IDictionary<IProperty, IProperty> columnMap)
        {
            Check.NotNull(sourceColumns, "sourceColumns");
            Check.NotNull(targetColumns, "targetColumns");
            Check.NotNull(columnMap, "columnMap");

            return
                sourceColumns.Count == targetColumns.Count
                && !sourceColumns.Where((t, i) => !EquivalentColumnReferences(t, targetColumns[i], columnMap)).Any();
        }

        protected virtual string ColumnType(IProperty property)
        {
            var extensions = ExtensionProvider.Extensions(property);

            return
                TypeMapper.GetTypeMapping(
                    extensions.ColumnType,
                    extensions.Column,
                    property.PropertyType,
                    property.IsKey() || property.IsForeignKey(),
                    property.IsConcurrencyToken)
                    .StoreTypeName;
        }

        protected virtual IReadOnlyList<ISequence> GetSequences([NotNull] IModel model)
        {
            return new ISequence[0];
        }
    }
}
