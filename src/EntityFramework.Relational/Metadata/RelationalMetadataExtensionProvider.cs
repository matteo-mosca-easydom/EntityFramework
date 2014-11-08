// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Metadata;

namespace Microsoft.Data.Entity.Relational.Metadata
{
    public class RelationalMetadataExtensionProvider : IRelationalMetadataExtensionProvider
    {
        public virtual IRelationalModelExtensions Extensions(IModel model)
        {
            return model.Relational();
        }

        public virtual IRelationalEntityTypeExtensions Extensions(IEntityType entityType)
        {
            return entityType.Relational();
        }

        public virtual IRelationalPropertyExtensions Extensions(IProperty property)
        {
            return property.Relational();
        }

        public virtual IRelationalKeyExtensions Extensions(IKey key)
        {
            return key.Relational();
        }

        public virtual IRelationalForeignKeyExtensions Extensions(IForeignKey foreignKey)
        {
            return foreignKey.Relational();
        }

        public virtual IRelationalIndexExtensions Extensions(IIndex index)
        {
            return index.Relational();
        }
    }
}
