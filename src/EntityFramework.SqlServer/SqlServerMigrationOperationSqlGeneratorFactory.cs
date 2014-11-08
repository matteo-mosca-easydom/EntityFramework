// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.SqlServer.Utilities;

namespace Microsoft.Data.Entity.SqlServer
{
    public class SqlServerMigrationOperationSqlGeneratorFactory : IMigrationOperationSqlGeneratorFactory
    {
        private readonly RelationalNameGenerator _nameGenerator;

        public SqlServerMigrationOperationSqlGeneratorFactory(
            [NotNull] RelationalNameGenerator nameGenerator)
        {
            Check.NotNull(nameGenerator, "nameGenerator");

            _nameGenerator = nameGenerator;
        }

        public virtual RelationalNameGenerator NameGenerator
        {
            get { return _nameGenerator; }
        }

        public virtual SqlServerMigrationOperationSqlGenerator Create()
        {
            return Create(new Model());
        }

        public virtual SqlServerMigrationOperationSqlGenerator Create([NotNull] IModel targetModel)
        {
            Check.NotNull(targetModel, "targetModel");

            return
                new SqlServerMigrationOperationSqlGenerator(
                    NameGenerator,
                    new SqlServerTypeMapper())
                    {
                        TargetModel = targetModel,
                    };
        }

        MigrationOperationSqlGenerator IMigrationOperationSqlGeneratorFactory.Create()
        {
            return Create();
        }

        MigrationOperationSqlGenerator IMigrationOperationSqlGeneratorFactory.Create(IModel targetModel)
        {
            return Create(targetModel);
        }
    }
}
