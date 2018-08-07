---
services: Container-Registry
platforms: dotnet
author: milismsft
---

# Getting started on managing Azure Container Registry with webhooks using C# #

          Azure Container Registry sample for managing container registry with webhooks.
             - Create an Azure Container Registry and setup couple Webhooks to be triggered on registry related actions (push, delete)
           - If a local Docker engine cannot be found, create a Linux virtual machine that will host a Docker engine
               to be used for this sample
           - Use Docker DotNet to create a Docker client that will push/pull an image to/from Azure Container Registry
           - Pull a test image from the public Docker repo (hello-world:latest) to be used as a sample for pushing/pulling
               to/from an Azure Container Registry
             - List the container registry webhook event notifications after pushing a container image to the registry


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-java/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/acr-dotnet-manage-azure-container-registry-with-webhooks.git

    cd acr-dotnet-manage-azure-container-registry-with-webhooks

    dotnet restore

    dotnet run

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.