using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CatalogApi.Helpers;
using CatalogApi.Infrastructure;
using CatalogApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace CatalogApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CatalogController : ControllerBase
    {
        private CatalogContext db;
        private IConfiguration _configuration;
        public CatalogController(CatalogContext db,IConfiguration configuration)
        {
            this.db = db;
            this._configuration = configuration;
        }
        [HttpGet("",Name ="GetProducts")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CatalogItem>>> GetProducts()
        {
            var result = await this.db.Catalog.FindAsync<CatalogItem>(FilterDefinition<CatalogItem>.Empty);
            return result.ToList();
        }

        [Authorize(Roles ="admin")]
        [HttpPost("", Name = "AddProduct")]
        [ProducesResponseType((int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public ActionResult<CatalogItem> AddProduct(CatalogItem item)
        {
            TryValidateModel(item);
            if (ModelState.IsValid)
            {
                this.db.Catalog.InsertOne(item);
                return Created("", item); //201
            }
            else
            {
                return BadRequest(ModelState);// 400
            }
            
        }

        [Authorize(Roles = "admin")]
        [HttpPost("product")]
        public ActionResult<CatalogItem> AddProduct()
        {
            //var imageName = SaveImageToLocal(Request.Form.Files[0]);
            var imageName = SaveImageToCloudAsync(Request.Form.Files[0]).GetAwaiter().GetResult();
            var catalogItem = new CatalogItem()
            {
                Name = Request.Form["name"],
                Price = Double.Parse(Request.Form["price"]),
                Quantity = Int32.Parse(Request.Form["quantity"]),
                ReorderLevel = Int32.Parse(Request.Form["reorderLevel"]),
                ManufacturingDate = DateTime.Parse(Request.Form["manufacturingDate"]),
                Vendors = new List<Vendor>(),
                ImageUrl = imageName
            };
            db.Catalog.InsertOne(catalogItem);
            BackupToTableAsync(catalogItem).GetAwaiter().GetResult();
            return catalogItem;
        }

        [HttpGet("{id}",Name ="FindById")]
        public async Task<ActionResult<CatalogItem>>FindProductById(string id)
        {
            var builder = Builders<CatalogItem>.Filter;
            var filter = builder.Eq("Id", id);
            var result = await db.Catalog.FindAsync(filter);
            var item = result.FirstOrDefault();
            if(item== null)
            {
                return NotFound(); // not found status code 400
            }
            else
            {
                return Ok(item);  // Not found status code 200
            }
        }

        //[HttpPost("product")]
        //public ActionResult<CatalogItem>AddProduct()
        //{
            
        //    var imageName = SaveimageToLocal(Request.Form.Files[0]);
        //    var catologItem = new CatalogItem()
        //    {
        //        Name = Request.Form["name"],
        //        Price = double.Parse(Request.Form["price"]),
        //        Quantity = Int32.Parse(Request.Form["quantity"]),
        //        ManufacturingDate = DateTime.Parse(Request.Form["manufacturingDate"]),
        //        ReorderLevel = Int32.Parse(Request.Form["reorderLevel"]),
        //    };
        //    db.Catalog.InsertOne(catalogItem);
        //    return catalogItem;
        //    }
        //[NonAction]
        //private string SaveimageToLocal(IFormFile image)
        //{
        //    var iamageName = $"{Guid.NewGuid()}_{image.FileName}";
        //    var image = Request.Form.Files[0];

        //    var catalogItem = new CatalogItem()
        //    {
        //        Name = Request.Form["name"],
        //        Price = double.Parse(Request.Form["price"]),
        //        Quantity = Int32.Parse(Request.Form["quantity"]),
        //        ReorderLevel = Int32.Parse(Request.Form["ReorderLevel"]),
        //        ManufacturingDate = DateTime.Parse(Request.Form["manufacturingDate"]),
        //        Vendors = new List<Vendor>(),
        //        ImageUrl = imageName
        //    };
           

        //    var dirName = Path.Combine(Directory.GetCurrentDirectory(), "Images");
        //    if (!Directory.Exists(dirName))
        //    {
        //        Directory.CreateDirectory(dirName);
        //    }
        //    var filePath = Path.Combine(dirName, imageName);
        //    using (FileStream fs = new FileStream(filePath, FileMode.Create))
        //    {
        //        image.CopyTo(fs);
        //    }
        [NonAction]
        private async Task<string> SaveImageToCloudAsync(IFormFile image)
        {
            var imagename = $"{Guid.NewGuid()}_{image.FileName}"; // image name must be unique
            var tempFile = Path.GetTempFileName();
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
            {
                await image.CopyToAsync(fs);
            }
            var imageFile = Path.Combine(Path.GetDirectoryName(tempFile), imagename);
            System.IO.File.Move(tempFile, imageFile); // renaming of temp file
            StorageAccountHelper storageHelper = new StorageAccountHelper();
            storageHelper.StorageConnectionString = _configuration.GetConnectionString("StorageConnection");
            var fileUrl = await storageHelper.UploadFileToBlobAsync(imageFile, "eshopimages");// uploading to cloud
            System.IO.File.Delete(imageFile); // delete local storage file after upload
            return fileUrl;
        }
        [NonAction]
        private async  Task<CatalogEntity> BackupToTableAsync(CatalogItem item)
        {
            StorageAccountHelper storageAccount = new StorageAccountHelper();
            //storageAccount.StorageConnectionString = _configuration.GetConnectionString("StorageConnection");
            //instead of storing on storage account backup stored in cosmos table api
            storageAccount.TableConjnectionString = _configuration.GetConnectionString("TableConnection");
            return await storageAccount.SaveToTableAsync(item);
        }
        }
      
    
}