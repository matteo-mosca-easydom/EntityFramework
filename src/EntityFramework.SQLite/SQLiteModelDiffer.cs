// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;

namespace Microsoft.Data.Entity.SQLite
{
    public class SQLiteModelDiffer : ModelDiffer
    {
        public SQLiteModelDiffer(
            [NotNull] RelationalMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalNameGenerator nameGenerator,
            [NotNull] SQLiteTypeMapper typeMapper,
            [NotNull] MigrationOperationFactory operationFactory,
            [NotNull] SQLiteMigrationOperationPreProcessor operationProcessor)
            : base(
                extensionProvider,
                nameGenerator,
                typeMapper,
                operationFactory,
                operationProcessor)
        {
        }

        public virtual new SQLiteTypeMapper TypeMapper
        {
            get { return (SQLiteTypeMapper)base.TypeMapper; }
        }

        public virtual new SQLiteMigrationOperationPreProcessor OperationProcessor
        {
            get { return (SQLiteMigrationOperationPreProcessor)base.OperationProcessor; }
        }
    }
}
