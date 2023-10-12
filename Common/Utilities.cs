// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.ResourceManager.Samples.Common
{
    public static class Utilities
    {
        public static Action<string> LoggerMethod { get; set; }
        public static Func<string> PauseMethod { get; set; }
        public static string ProjectPath { get; set; }
        private static Random _random => new Random();

        private static readonly string SshKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQCfSPC2K7LZcFKEO+/t3dzmQYtrJFZNxOsbVgOVKietqHyvmYGHEC0J2wPdAqQ/63g/hhAEFRoyehM+rbeDri4txB3YFfnOK58jqdkyXzupWqXzOrlKY4Wz9SKjjN765+dqUITjKRIaAip1Ri137szRg71WnrmdP3SphTRlCx1Bk2nXqWPsclbRDCiZeF8QOTi4JqbmJyK5+0UqhqYRduun8ylAwKKQJ1NJt85sYIHn9f1Rfr6Tq2zS0wZ7DHbZL+zB5rSlAr8QyUdg/GQD+cmSs6LvPJKL78d6hMGk84ARtFo4A79ovwX/Fj01znDQkU6nJildfkaolH2rWFG/qttD azjava@javalib.Com";

        static Utilities()
        {
            LoggerMethod = Console.WriteLine;
            PauseMethod = Console.ReadLine;
            ProjectPath = ".";
        }

        public static void Log(string message)
        {
            LoggerMethod.Invoke(message);
        }

        public static void Log(object obj)
        {
            if (obj != null)
            {
                LoggerMethod.Invoke(obj.ToString());
            }
            else
            {
                LoggerMethod.Invoke("(null)");
            }
        }

        public static void Log()
        {
            Utilities.Log("");
        }

        public static string ReadLine() => PauseMethod.Invoke();

        public static string CreateRandomName(string namePrefix) => $"{namePrefix}{_random.Next(9999)}";

        public static string CreatePassword() => "azure12345QWE!";

        public static string CreateUsername() => "tirekicker";

        public static async Task<List<T>> ToEnumerableAsync<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            List<T> list = new List<T>();
            await foreach (T item in asyncEnumerable)
            {
                list.Add(item);
            }
            return list;
        }

        public static async Task<PublicIPAddressResource> CreatePublicIP(ResourceGroupResource resourceGroup)
        {
            string publicIPName = CreateRandomName("pip");

            var publicIPInput = new PublicIPAddressData()
            {
                Location = resourceGroup.Data.Location,
                PublicIPAllocationMethod = NetworkIPAllocationMethod.Static,
            };
            var publicIPLro = await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(WaitUntil.Completed, publicIPName, publicIPInput);
            return publicIPLro.Value;
        }

        public static async Task<NetworkInterfaceResource> CreateNetworkInterface(ResourceGroupResource resourceGroup, ResourceIdentifier publicIPId)
        {
            string vnetName = CreateRandomName("vnet");
            string nicName = CreateRandomName("nic");

            // Create Network
            VirtualNetworkData vnetInput = new VirtualNetworkData()
            {
                Location = resourceGroup.Data.Location,
                AddressPrefixes = { "10.10.0.0/16" },
                Subnets =
                {
                    new SubnetData() { Name = "default", AddressPrefix = "10.10.1.0/24" },
                    new SubnetData() { Name = "subnet1", AddressPrefix = "10.10.2.0/24" },
                    new SubnetData() { Name = "subnet2", AddressPrefix = "10.10.3.0/24" }
                }
            };
            var vnetLro = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetInput);
            VirtualNetworkResource vnet = vnetLro.Value;

            // Create a network interface
            var subnetInput = new NetworkInterfaceData()
            {
                Location = resourceGroup.Data.Location,
                IPConfigurations =
                    {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "default-config",
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Subnet = new SubnetData()
                            {
                                Id = vnet.Data.Subnets[0].Id,
                            },
                            PublicIPAddress = new PublicIPAddressData()
                            {
                                Id = publicIPId
                            },
                        }
                    }
            };
            var networkInterfaceLro = await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(WaitUntil.Completed, nicName, subnetInput);
            return networkInterfaceLro.Value;
        }

        public static async Task<PublicIPAddressResource> CreatePublicIP(ResourceGroupResource resourceGroup, string publicIpName)
        {
            publicIpName = string.IsNullOrEmpty(publicIpName) ? CreateRandomName("pip") : publicIpName;

            Utilities.Log("Creating a public IP address...");
            PublicIPAddressData publicIPInput = new PublicIPAddressData()
            {
                Location = resourceGroup.Data.Location,
                Sku = new PublicIPAddressSku()
                {
                    Name = PublicIPAddressSkuName.Standard,
                    Tier = PublicIPAddressSkuTier.Regional
                },
                PublicIPAllocationMethod = NetworkIPAllocationMethod.Static,
                DnsSettings = new PublicIPAddressDnsSettings { DomainNameLabel = publicIpName },
            };
            _ = await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(WaitUntil.Completed, publicIpName, publicIPInput);
            var publicIPLro = await resourceGroup.GetPublicIPAddresses().GetAsync(publicIpName);
            Utilities.Log($"Created a public IP address: {publicIPLro.Value.Data.Name}");
            return publicIPLro.Value;
        }

        public static VirtualMachineData GetDefaultVMInputData(ResourceGroupResource resourceGroup, string vmName) =>
            new VirtualMachineData(resourceGroup.Data.Location)
            {
                HardwareProfile = new VirtualMachineHardwareProfile() { VmSize = VirtualMachineSizeType.StandardB4Ms },
                StorageProfile = new VirtualMachineStorageProfile()
                {
                    OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                    {
                        OSType = SupportedOperatingSystemType.Linux,
                        Caching = CachingType.ReadWrite,
                        ManagedDisk = new VirtualMachineManagedDisk()
                        {
                            StorageAccountType = StorageAccountType.StandardLrs
                        }
                    },
                    ImageReference = new ImageReference()
                    {
                        Publisher = "Canonical",
                        Offer = "UbuntuServer",
                        Sku = "16.04-LTS",
                        Version = "latest",
                    }
                },
                OSProfile = new VirtualMachineOSProfile()
                {
                    AdminUsername = CreateUsername(),
                    AdminPassword = CreatePassword(),
                    ComputerName = vmName,
                },
                NetworkProfile = new VirtualMachineNetworkProfile() { }
            };
    }
}