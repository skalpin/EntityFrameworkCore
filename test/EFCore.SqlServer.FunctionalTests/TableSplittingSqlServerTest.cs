// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore
{
    public class TableSplittingSqlServerTest : TableSplittingTestBase
    {
        public TableSplittingSqlServerTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        protected override ITestStoreFactory TestStoreFactory => SqlServerTestStoreFactory.Instance;

        public override void Can_use_with_redundant_relationships()
        {
            base.Can_use_with_redundant_relationships();

            // TODO: [Name] shouldn't be selected multiple times and left joins are not needed
            AssertSql(
                @"SELECT [v].[Name], [v].[Discriminator], [v].[SeatingCapacity], [t].[Name], [t].[Operator_Discriminator], [t].[Operator_Name], [t].[LicenseType], [t0].[Name], [t0].[Description], [t0].[Engine_Discriminator], [t1].[Name], [t1].[Capacity], [t1].[FuelTank_Discriminator], [t1].[FuelType], [t1].[GrainGeometry]
FROM [Vehicles] AS [v]
LEFT JOIN (
    SELECT [v0].[Name], [v0].[Operator_Discriminator], [v0].[Operator_Name], [v0].[LicenseType], [v0].[Name] AS [Name0], [v0].[Discriminator], [v0].[SeatingCapacity]
    FROM [Vehicles] AS [v0]
    WHERE [v0].[Discriminator] IN (N'Vehicle', N'PoweredVehicle') AND [v0].[Operator_Discriminator] IN (N'Operator', N'LicensedOperator')
) AS [t] ON [v].[Name] = [t].[Name]
LEFT JOIN (
    SELECT [v1].[Name], [v1].[Description], [v1].[Engine_Discriminator], [v1].[Name] AS [Name0], [v1].[Discriminator], [v1].[SeatingCapacity]
    FROM [Vehicles] AS [v1]
    WHERE (([v1].[Discriminator] = N'PoweredVehicle') AND [v1].[Discriminator] IS NOT NULL) AND [v1].[Engine_Discriminator] IN (N'Engine', N'ContinuousCombustionEngine', N'IntermittentCombustionEngine', N'SolidRocket')
) AS [t0] ON [v].[Name] = [t0].[Name]
LEFT JOIN (
    SELECT [v2].[Name], [v2].[Capacity], [v2].[FuelTank_Discriminator], [v2].[FuelType], [v2].[GrainGeometry], [v2].[Name] AS [Name0], [v2].[Discriminator], [v2].[SeatingCapacity], [v2].[Description], [v2].[Engine_Discriminator]
    FROM [Vehicles] AS [v2]
    WHERE ((([v2].[Discriminator] = N'PoweredVehicle') AND [v2].[Discriminator] IS NOT NULL) AND [v2].[FuelTank_Discriminator] IN (N'FuelTank', N'SolidFuelTank')) OR (((([v2].[Discriminator] = N'PoweredVehicle') AND [v2].[Discriminator] IS NOT NULL) AND [v2].[Engine_Discriminator] IN (N'ContinuousCombustionEngine', N'IntermittentCombustionEngine', N'SolidRocket')) AND [v2].[FuelTank_Discriminator] IN (N'FuelTank', N'SolidFuelTank'))
) AS [t1] ON [t0].[Name] = [t1].[Name]
WHERE [v].[Discriminator] IN (N'Vehicle', N'PoweredVehicle')
ORDER BY [v].[Name]");
        }

        public override void Can_use_with_chained_relationships()
        {
            base.Can_use_with_chained_relationships();
        }

        public override void Can_use_with_fanned_relationships()
        {
            base.Can_use_with_fanned_relationships();
        }

        public override void Can_query_shared()
        {
            base.Can_query_shared();

            AssertSql(
                @"SELECT [v].[Name], [v].[Operator_Discriminator], [v].[Operator_Name], [v].[LicenseType]
FROM [Vehicles] AS [v]
WHERE [v].[Discriminator] IN (N'Vehicle', N'PoweredVehicle') AND [v].[Operator_Discriminator] IN (N'Operator', N'LicensedOperator')");
        }

        public override void Can_query_shared_derived_hierarchy()
        {
            base.Can_query_shared_derived_hierarchy();

            AssertSql(
                @"SELECT [v].[Name], [v].[Capacity], [v].[FuelTank_Discriminator], [v].[FuelType], [v].[GrainGeometry]
FROM [Vehicles] AS [v]
WHERE ((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND [v].[FuelTank_Discriminator] IN (N'FuelTank', N'SolidFuelTank')) OR (((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND [v].[Engine_Discriminator] IN (N'ContinuousCombustionEngine', N'IntermittentCombustionEngine', N'SolidRocket')) AND [v].[FuelTank_Discriminator] IN (N'FuelTank', N'SolidFuelTank'))");
        }

        public override void Can_query_shared_derived_nonhierarchy()
        {
            base.Can_query_shared_derived_nonhierarchy();
            AssertSql(
                @"SELECT [v].[Name], [v].[Capacity], [v].[FuelType]
FROM [Vehicles] AS [v]
WHERE ((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND ([v].[FuelType] IS NOT NULL OR [v].[Capacity] IS NOT NULL)) OR (((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND [v].[Engine_Discriminator] IN (N'ContinuousCombustionEngine', N'IntermittentCombustionEngine', N'SolidRocket')) AND ([v].[FuelType] IS NOT NULL OR [v].[Capacity] IS NOT NULL))");
        }

        public override void Can_query_shared_derived_nonhierarchy_all_required()
        {
            base.Can_query_shared_derived_nonhierarchy_all_required();

            AssertSql(
                @"SELECT [v].[Name], [v].[Capacity], [v].[FuelType]
FROM [Vehicles] AS [v]
WHERE ((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND ([v].[FuelType] IS NOT NULL AND [v].[Capacity] IS NOT NULL)) OR (((([v].[Discriminator] = N'PoweredVehicle') AND [v].[Discriminator] IS NOT NULL) AND [v].[Engine_Discriminator] IN (N'ContinuousCombustionEngine', N'IntermittentCombustionEngine', N'SolidRocket')) AND ([v].[FuelType] IS NOT NULL AND [v].[Capacity] IS NOT NULL))");
        }

        public override void Can_change_dependent_instance_non_derived()
        {
            base.Can_change_dependent_instance_non_derived();

            AssertSql(
                @"@p3='Trek Pro Fit Madone 6 Series' (Nullable = false) (Size = 450)
@p0='LicensedOperator' (Nullable = false) (Size = 4000)
@p1='repairman' (Size = 4000)
@p2='Repair' (Size = 4000)

SET NOCOUNT ON;
UPDATE [Vehicles] SET [Operator_Discriminator] = @p0, [Operator_Name] = @p1, [LicenseType] = @p2
WHERE [Name] = @p3;
SELECT @@ROWCOUNT;");
        }

        public override void Can_change_principal_instance_non_derived()
        {
            base.Can_change_principal_instance_non_derived();

            AssertSql(
                @"@p1='Trek Pro Fit Madone 6 Series' (Nullable = false) (Size = 450)
@p0='2'

SET NOCOUNT ON;
UPDATE [Vehicles] SET [SeatingCapacity] = @p0
WHERE [Name] = @p1;
SELECT @@ROWCOUNT;");
        }
    }
}
