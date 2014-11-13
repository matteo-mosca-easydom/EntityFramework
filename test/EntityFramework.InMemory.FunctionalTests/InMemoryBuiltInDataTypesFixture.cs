// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.FunctionalTests;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;

namespace Microsoft.Data.Entity.InMemory.FunctionalTests
{
    public class InMemoryBuiltInDataTypesFixture : BuiltInDataTypesFixtureBase<InMemoryTestStore>
    {
        private readonly IServiceProvider _serviceProvider;

        public InMemoryBuiltInDataTypesFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryStore()
                .ServiceCollection
                .AddTestModelSource(OnModelCreating)
                .BuildServiceProvider();
        }

        public override InMemoryTestStore CreateTestStore()
        {
            return new InMemoryTestStore();
        }

        public override DbContext CreateContext(InMemoryTestStore testStore)
        {
            var options = new DbContextOptions()
                .UseInMemoryStore();

            return new DbContext(_serviceProvider, options);
        }
    }
}
