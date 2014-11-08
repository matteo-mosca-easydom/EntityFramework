// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.SqlServer.Metadata;
using Microsoft.Data.Entity.SqlServer.Utilities;

namespace Microsoft.Data.Entity.SqlServer
{
    public class SqlServerModelDiffer : ModelDiffer
    {
        public SqlServerModelDiffer(
            [NotNull] SqlServerMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalNameGenerator nameGenerator,
            [NotNull] SqlServerTypeMapper typeMapper,
            [NotNull] MigrationOperationFactory operationFactory,
            [NotNull] SqlServerMigrationOperationPreProcessor operationProcessor)
            : base(
                extensionProvider,
                nameGenerator,
                typeMapper,
                operationFactory,
                operationProcessor)
        {
        }

        public virtual new SqlServerMetadataExtensionProvider ExtensionProvider
        {
            get { return (SqlServerMetadataExtensionProvider)base.ExtensionProvider; }
        }

        public virtual new SqlServerTypeMapper TypeMapper
        {
            get { return (SqlServerTypeMapper)base.TypeMapper; }
        }

        public virtual new SqlServerMigrationOperationPreProcessor OperationProcessor
        {
            get { return (SqlServerMigrationOperationPreProcessor)base.OperationProcessor; }
        }

        protected override IReadOnlyList<ISequence> GetSequences(IModel model)
        {
            Check.NotNull(model, "model");

            return
                model.EntityTypes
                    .SelectMany(t => t.Properties)
                    .Select(p => p.SqlServer().TryGetSequence())
                    .Where(s => s != null)
                    .Distinct((x, y) => x.Name == y.Name && x.Schema == y.Schema)
                    .ToList();
        }
    }
}
