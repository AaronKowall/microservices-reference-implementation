// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Fabrikam.DroneDelivery.DroneSchedulerService.Models;
using Fabrikam.DroneDelivery.DroneSchedulerService.Services;
using Fabrikam.DroneDelivery.DroneSchedulerService.Tests.Utils;

namespace Fabrikam.DroneDelivery.DroneSchedulerService.Tests
{
    public class CosmosDBRespositoryTests
    {
        private readonly ILogger<CosmosRepository<InternalDroneUtilization>> _loggerDebug;

        private readonly IDocumentClient _clientMockObject;
        private readonly IOptions<CosmosDBRepositoryOptions<InternalDroneUtilization>> _optionsMockObject;
        private readonly ICosmosDBRepositoryMetricsTracker<InternalDroneUtilization> _metricsTrackerMockObject;

        private readonly IQueryable<InternalDroneUtilization> _fakeResults;

        public CosmosDBRespositoryTests()
        {
            var servicesBuilder = new ServiceCollection();
            servicesBuilder.AddLogging(logging => logging.AddDebug());
            var services = servicesBuilder.BuildServiceProvider();

            _loggerDebug = services.GetService<
                ILogger<
                    CosmosRepository<
                        InternalDroneUtilization>>>();

            _fakeResults = new List<InternalDroneUtilization> {
                new InternalDroneUtilization {
                    Id = "d0001",
                    PartitionKey = "o00042",
                    OwnerId = "o00042",
                    Month = 6,
                    Year = 2019,
                    TraveledMiles =10d,
                    AssignedHours=1d,
                    DocumentType = typeof(InternalDroneUtilization).Name
                },
                new InternalDroneUtilization {
                    Id = "d0002",
                    PartitionKey = "o00042",
                    OwnerId = "o00042",
                    Month = 6,
                    Year = 2019,
                    TraveledMiles=32d,
                    AssignedHours=2d,
                    DocumentType = typeof(InternalDroneUtilization).Name
                }
            }.AsQueryable();

            _clientMockObject = DocumentClientMock.
                CreateDocumentClientMockObject(_fakeResults);

            var fakeOptionsValue =
                new CosmosDBRepositoryOptions<InternalDroneUtilization>
                {
                    CollectionUri = UriFactory.CreateDocumentCollectionUri(
                        "fakeDb",
                        "fakeCol")
                };

            var optionsMock = new Mock<
                IOptions<
                    CosmosDBRepositoryOptions<
                        InternalDroneUtilization>>>();
            optionsMock
                .Setup(o => o.Value)
                .Returns(fakeOptionsValue);

            _optionsMockObject = optionsMock.Object;

            _metricsTrackerMockObject =
                Mock.Of<ICosmosDBRepositoryMetricsTracker<InternalDroneUtilization>>(
                    t => t.GetQueryMetricsTracker(It.IsAny<string>(), It.IsAny<string>())
                            == Mock.Of<ICosmosDBRepositoryQueryMetricsTracker<InternalDroneUtilization>>());
        }

        [Fact]
        public async Task WhenGetItemsAsyncWithPartitionId_ThenClientMakesAQueryWithPartitionId()
        {
            // Arrange
            string ownerId = "o00042";

            var repo = new CosmosRepository<InternalDroneUtilization>(
                _clientMockObject,
                _optionsMockObject,
                _loggerDebug,
                _metricsTrackerMockObject);

            // Act
            var res = await repo.GetItemsAsync(
                p => true,
                ownerId);

            // Assert
            Assert.NotNull(res);
            Assert.Equal(_fakeResults.Count(), res.Count());
            Assert.All(
                res,
                r =>
                {
                    Assert.Equal(ownerId, r.PartitionKey);
                    Assert.Equal(typeof(InternalDroneUtilization).Name, r.DocumentType);
                });
            Mock.Get(_clientMockObject)
                .Verify(dc =>
                    dc.CreateDocumentQuery<InternalDroneUtilization>(
                        It.IsAny<Uri>(),
                        It.Is<FeedOptions>(fo =>
                            fo.PartitionKey != null
                            && fo.PartitionKey.ToString().Contains(ownerId)
                            && fo.EnableCrossPartitionQuery == false)));
        }

        [Fact]
        public async Task WhenGetItemsAsyncWithoutPartitionId_ThenClientMakesAQueryWithoutPartitionIdAndEnablesCrossPartition()
        {
            // Arrange
            string ownerId = "o00042";

            var repo = new CosmosRepository<InternalDroneUtilization>(
                _clientMockObject,
                _optionsMockObject,
                _loggerDebug,
                _metricsTrackerMockObject);

            // Act
            var res = await repo.GetItemsAsync(
                p => true,
                null);

            // Assert
            Assert.NotNull(res);
            Assert.Equal(_fakeResults.Count(), res.Count());
            Assert.All(
                res,
                r =>
                {
                    Assert.Equal(ownerId, r.PartitionKey);
                    Assert.Equal(typeof(InternalDroneUtilization).Name, r.DocumentType);
                });
            Mock.Get(_clientMockObject)
                .Verify(dc =>
                    dc.CreateDocumentQuery<InternalDroneUtilization>(
                        It.IsAny<Uri>(),
                        It.Is<FeedOptions>(fo =>
                            fo.PartitionKey == null
                            && fo.EnableCrossPartitionQuery == true)));
        }
    }
}
