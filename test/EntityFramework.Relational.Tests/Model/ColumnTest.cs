// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Relational.Utilities;
using Xunit;

namespace Microsoft.Data.Entity.Relational.Tests.Model
{
    public class ColumnTest
    {
        [Fact]
        public void Create_and_initialize_column()
        {
            var column = new Column("Foo", "int")
                { IsNullable = true, DefaultValue = 5 };

            Assert.Equal("Foo", column.Name);
            Assert.Null(column.ClrType);
            Assert.Equal("int", column.DataType);
            Assert.True(column.IsNullable);
            Assert.Equal(5, column.DefaultValue);
            Assert.Null(column.DefaultSql);

            column = new Column("Bar", typeof(int), null)
                { IsNullable = false, DefaultSql = "GETDATE()" };

            Assert.Equal("Bar", column.Name);
            Assert.Same(typeof(int), column.ClrType);
            Assert.Null(column.DataType);
            Assert.False(column.IsNullable);
            Assert.Null(column.DefaultValue);
            Assert.Equal("GETDATE()", column.DefaultSql);
        }

        [Fact]
        public void Can_set_name()
        {
            var column = new Column("Foo", typeof(int));

            Assert.Equal("Foo", column.Name);

            column.Name = "Bar";

            Assert.Equal("Bar", column.Name);
        }
    }
}
