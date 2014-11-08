// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational.Metadata;

namespace Microsoft.Data.Entity.SqlServer.Metadata
{
    public class SqlServerMetadataExtensionProvider : IRelationalMetadataExtensionProvider
    {
        public virtual ISqlServerModelExtensions Extensions(IModel model)
        {
            return model.SqlServer();
        }

        public virtual ISqlServerEntityTypeExtensions Extensions(IEntityType entityType)
        {
            return entityType.SqlServer();
        }

        public virtual ISqlServerPropertyExtensions Extensions(IProperty property)
        {
            return property.SqlServer();
        }

        public virtual ISqlServerKeyExtensions Extensions(IKey key)
        {
            return key.SqlServer();
        }

        public virtual ISqlServerForeignKeyExtensions Extensions(IForeignKey foreignKey)
        {
            return foreignKey.SqlServer();
        }

        public virtual ISqlServerIndexExtensions Extensions(IIndex index)
        {
            return index.SqlServer();
        }

        IRelationalModelExtensions IRelationalMetadataExtensionProvider.Extensions(IModel model)
        {
            return Extensions(model);
        }

        IRelationalEntityTypeExtensions IRelationalMetadataExtensionProvider.Extensions(IEntityType entityType)
        {
            return Extensions(entityType);
        }

        IRelationalPropertyExtensions IRelationalMetadataExtensionProvider.Extensions(IProperty property)
        {
            return Extensions(property);
        }

        IRelationalKeyExtensions IRelationalMetadataExtensionProvider.Extensions(IKey key)
        {
            return Extensions(key);
        }

        IRelationalForeignKeyExtensions IRelationalMetadataExtensionProvider.Extensions(IForeignKey foreignKey)
        {
            return Extensions(foreignKey);
        }

        IRelationalIndexExtensions IRelationalMetadataExtensionProvider.Extensions(IIndex index)
        {
            return Extensions(index);
        }
    }
}
