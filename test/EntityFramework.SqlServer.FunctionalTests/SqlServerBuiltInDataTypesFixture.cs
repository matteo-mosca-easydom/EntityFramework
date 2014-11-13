// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.FunctionalTests;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class SqlServerBuiltInDataTypesFixture : BuiltInDataTypesFixtureBase<SqlServerTestStore>
    {
        private readonly IServiceProvider _serviceProvider;

        public SqlServerBuiltInDataTypesFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer()
                .ServiceCollection
                .AddTestModelSource(OnModelCreating)
                .BuildServiceProvider();
        }

        public override SqlServerTestStore CreateTestStore()
        {
            return SqlServerTestStore.CreateScratchAsync().Result;
        }

        public override DbContext CreateContext(SqlServerTestStore testStore)
        {
            var options = new DbContextOptions()
                .UseSqlServer(testStore.Connection);

            var context = new DbContext(_serviceProvider, options);
            context.Database.EnsureCreated();
            context.Database.AsRelational().Connection.UseTransaction(testStore.Transaction);
            return context;
        }

        public override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BuiltInNonNullableDataTypes>(b =>
            {
                b.Ignore(dt => dt.TestInt16);
                b.Ignore(dt => dt.TestUnsignedInt16);
                b.Ignore(dt => dt.TestUnsignedInt32);
                b.Ignore(dt => dt.TestUnsignedInt64);
                b.Ignore(dt => dt.TestCharacter);
                b.Ignore(dt => dt.TestSignedByte);
            });

            modelBuilder.Entity<BuiltInNullableDataTypes>(b =>
            {
                b.Ignore(dt => dt.TestNullableInt16);
                b.Ignore(dt => dt.TestNullableUnsignedInt16);
                b.Ignore(dt => dt.TestNullableUnsignedInt32);
                b.Ignore(dt => dt.TestNullableUnsignedInt64);
                b.Ignore(dt => dt.TestNullableCharacter);
                b.Ignore(dt => dt.TestNullableSignedByte);
            });
        }
    }
}
