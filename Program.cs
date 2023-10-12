// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Net;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute;
using System.Net.NetworkInformation;
using Docker.DotNet;
using Azure.ResourceManager.ContainerRegistry.Models;
using Azure.ResourceManager.ContainerRegistry;
using System;

namespace ManageContainerRegistryWithWebhooks
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;

        /**
         * Azure Container Registry sample for managing container registry with webhooks.
         *    - Create an Azure Container Registry and setup couple Webhooks to be triggered on registry related actions (push, delete)
         *  - If a local Docker engine cannot be found, create a Linux virtual machine that will host a Docker engine
         *      to be used for this sample
         *  - Use Docker DotNet to create a Docker client that will push/pull an image to/from Azure Container Registry
         *  - Pull a test image from the public Docker repo (hello-world:latest) to be used as a sample for pushing/pulling
         *      to/from an Azure Container Registry
         *    - List the container registry webhook event notifications after pushing a container image to the registry
         */
        public static async Task RunSample(ArmClient client)
        {
            string rgName = Utilities.CreateRandomName("ACRTemplateRG");
            string acrName = Utilities.CreateRandomName("acrsample");
            string saName = Utilities.CreateRandomName("sa");
            string dockerImageName = "hello-world";
            string dockerImageTag = "latest";
            string dockerContainerName = "sample-hello";
            string dockerImageRelPath = "samplesdotnet";
            string webhookName1 = "webhookbing1";
            string webhookName2 = "webhookbing2";
            string webhookServiceUri1 = "https://www.bing.com";
            string webhookServiceUri2 = "https://www.bing.com";

            try
            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                Utilities.Log($"creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //=============================================================
                // Create an Azure Container Registry to store and manage private Docker container images

                Utilities.Log("Creating an Azure Container Registry");

                // Create a azure container registry 
                var registryData = new ContainerRegistryData(resourceGroup.Data.Location, new ContainerRegistrySku(ContainerRegistrySkuName.Premium))
                {
                    IsAdminUserEnabled = true,
                    Sku = new ContainerRegistrySku(ContainerRegistrySkuName.Basic),
                    Tags =
                    {
                        { "key1","value1"},
                        { "key2","value2"}
                    }
                };
                var lro = await resourceGroup.GetContainerRegistries().CreateOrUpdateAsync(WaitUntil.Completed, acrName, registryData);
                ContainerRegistryResource containerRegistry = lro.Value;




                // Create two webhooks
                Utilities.Log("Creating two webhooks...");
                var webhookInput1 = new ContainerRegistryWebhookCreateOrUpdateContent(resourceGroup.Data.Location)
                {
                    Actions =
                    {
                        ContainerRegistryWebhookAction.Push,
                        ContainerRegistryWebhookAction.Delete
                    },
                    ServiceUri = new Uri(webhookServiceUri1),
                    Tags = { { "key1", "value1" }, { "key2", "value2" } },
                    CustomHeaders = { { "name", "value" } },
                };
                var webhookLro1 = await containerRegistry.GetContainerRegistryWebhooks().CreateOrUpdateAsync(WaitUntil.Completed, webhookName1, webhookInput1);
                ContainerRegistryWebhookResource webhook1 = webhookLro1.Value;
                Utilities.Log($"Created webhook: {webhook1.Data.Name}");

                var webhookInput2 = new ContainerRegistryWebhookCreateOrUpdateContent(resourceGroup.Data.Location)
                {
                    Actions =
                    {
                        ContainerRegistryWebhookAction.Push,
                    },
                    ServiceUri = new Uri(webhookServiceUri2),
                    Status = ContainerRegistryWebhookStatus.Disabled,
                    Scope = "",
                };
                var webhookLro2 = await containerRegistry.GetContainerRegistryWebhooks().CreateOrUpdateAsync(WaitUntil.Completed, webhookName2, webhookInput2);
                ContainerRegistryWebhookResource webhook2 = webhookLro2.Value;
                Utilities.Log($"Created webhook: {webhook2.Data.Name}");

                //=============================================================
                // Ping the container registry webhook to validate it works as expected

                _ = await webhook1.PingAsync();
                var webhookEvents = await webhook1.GetEventsAsync().ToEnumerableAsync();
                Utilities.Log($"Found {webhookEvents.Count()} webhook events for: {webhook1.Data.Name} with container service: {containerRegistry.Data.Name}/n");
                foreach (var webhookEventInfo in webhookEvents)
                {
                    Utilities.Log($"\t{webhookEventInfo.EventResponseMessage.Content}");
                }


                //=============================================================
                // Create a Docker client that will be used to push/pull images to/from the Azure Container Registry

                var acrCredentials = await containerRegistry.GetCredentialsAsync();

                using (DockerClient dockerClient = await DockerUtils.CreateDockerClient(resourceGroup))
                {
                    var pullImgResult = dockerClient.Images.PullImage(
                        new Docker.DotNet.Models.ImagesPullParameters()
                        {
                            Parent = dockerImageName,
                            Tag = dockerImageTag
                        },
                        new Docker.DotNet.Models.AuthConfig());

                    Utilities.Log("List Docker images for: " + dockerClient.Configuration.EndpointBaseUri.AbsoluteUri);
                    var listImages = dockerClient.Images.ListImages(
                        new Docker.DotNet.Models.ImagesListParameters()
                        {
                            All = true
                        });
                    foreach (var img in listImages)
                    {
                        Utilities.Log("\tFound image " + img.RepoTags[0] + " (id:" + img.ID + ")");
                    }

                    var createContainerResult = dockerClient.Containers.CreateContainer(
                        new Docker.DotNet.Models.CreateContainerParameters()
                        {
                            Name = dockerContainerName,
                            Image = dockerImageName + ":" + dockerImageTag
                        });
                    Utilities.Log("List Docker containers for: " + dockerClient.Configuration.EndpointBaseUri.AbsoluteUri);
                    var listContainers = dockerClient.Containers.ListContainers(
                        new Docker.DotNet.Models.ContainersListParameters()
                        {
                            All = true
                        });
                    foreach (var container in listContainers)
                    {
                        Utilities.Log("\tFound container " + container.Names[0] + " (id:" + container.ID + ")");
                    }

                    //=============================================================
                    // Commit the new container

                    string privateRepoUrl = containerRegistry.Data.LoginServer + "/" + dockerImageRelPath + "/" + dockerContainerName;
                    Utilities.Log("Commiting image at: " + privateRepoUrl);

                    var commitContainerResult = dockerClient.Miscellaneous.CommitContainerChanges(
                        new Docker.DotNet.Models.CommitContainerChangesParameters()
                        {
                            ContainerID = dockerContainerName,
                            RepositoryName = privateRepoUrl,
                            Tag = dockerImageTag
                        });

                    //=============================================================
                    // Push the new Docker image to the Azure Container Registry

                    var pushImageResult = dockerClient.Images.PushImage(privateRepoUrl,
                        new Docker.DotNet.Models.ImagePushParameters()
                        {
                            ImageID = privateRepoUrl,
                            Tag = dockerImageTag
                        },
                        new Docker.DotNet.Models.AuthConfig()
                        {
                            Username = acrCredentials.Value.Username,
                            Password = acrCredentials.Value.Passwords.First().Value,
                            ServerAddress = containerRegistry.Data.LoginServer
                        });

                    //=============================================================
                    // Gets the container registry webhook after pushing a container image and lists the event notifications

                    webhook1 = await containerRegistry.GetContainerRegistryWebhooks().GetAsync(webhookName1);
                    webhookEvents = await webhook1.GetEventsAsync().ToEnumerableAsync();
                    Utilities.Log($"Found {webhookEvents.Count()} webhook events for: {webhook1.Data.Name} with container service: {containerRegistry.Data.Name}/n");
                    foreach (var webhookEventInfo in webhookEvents)
                    {
                        Utilities.Log($"\t{webhookEventInfo.EventResponseMessage.Content}");
                    }
                }
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (Exception)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}