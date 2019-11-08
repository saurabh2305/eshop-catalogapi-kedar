using CatalogApi.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CatalogApi.Helpers
{
    public class StorageAccountHelper
    {
        private string storageConnectionString;
        private string tableConnectionString;
        private CloudStorageAccount storageAccount;
        private CloudBlobClient blobClient;
        private CloudTableClient tableClient;
        private CloudStorageAccount tableStorageAccount;

        public string StorageConnectionString
        {
            get { return storageConnectionString; }

            set
            {
                this.storageConnectionString = value;
                storageAccount = CloudStorageAccount.Parse(this.storageConnectionString);
                // reads connectionstring and returns storageaccount
            }
        }

        public string TableConjnectionString
        {
            get { return tableConnectionString; }
            set
            {
                this.tableConnectionString = value;
                tableStorageAccount = CloudStorageAccount.Parse(this.tableConnectionString);
            }
        }
        public async Task<string> UploadFileToBlobAsync(string filePath, string containerName)
        {
            blobClient = storageAccount.CreateCloudBlobClient();
            // blob client is used to work with blob service
            var container = blobClient.GetContainerReference(containerName);// container not exists create
            await container.CreateIfNotExistsAsync();
            BlobContainerPermissions permissions = new BlobContainerPermissions()
            {
                PublicAccess = BlobContainerPublicAccessType.Container
            };

            await container.SetPermissionsAsync(permissions);
            var filename = Path.GetFileName(filePath);
            var blob = container.GetBlockBlobReference(filename);
            await blob.DeleteIfExistsAsync();// delete blob if exist already with same name
            await blob.UploadFromFileAsync(filePath);
            return blob.Uri.AbsoluteUri;   // returns url of blob file
        }

        public async Task<CatalogEntity> SaveToTableAsync(CatalogItem item)
        // CALL THIS METHOD AFTER ADDING DATA TO MONGO PRIMARY DATABASE
        {
            CatalogEntity catalogEntity = new CatalogEntity(item.Name, item.Id)
            {
                ImageUrl = item.ImageUrl,
                ReorderLevel = item.ReorderLevel,
                Quantity = item.Quantity,
                ManufacturingDate = item.ManufacturingDate
            };
            //tableClient = storageAccount.CreateCloudTableClient();
            tableClient = tableStorageAccount.CreateCloudTableClient();
            var catalogTable = tableClient.GetTableReference("catalog");
            await catalogTable.CreateIfNotExistsAsync();
            TableOperation operation = TableOperation.InsertOrMerge(catalogEntity);
            var result = await catalogTable.ExecuteAsync(operation);
            return result.Result as CatalogEntity;
        }

    }
}
