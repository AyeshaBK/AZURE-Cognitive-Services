using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ImageResizer;
using AppDev3BProj.Models;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;
using System.IO;
using Microsoft.ProjectOxford.Vision;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AppDev3BProj.Controllers
{
    public class BlobController : Controller
    {
        //const string subscriptionKey = "b75037b51a744c1eafeaeb1e5e8938fb";
        //const string uriBase = "https://westus.api.cognitive.microsoft.com/vision/v1.0/analyze";
        //private dynamic imageResult;

        public ActionResult Home()
        {
            return View();
        }

        private bool HasMatchingMetadata(CloudBlockBlob blob, string term)
        {
            foreach (var item in blob.Metadata)
            {
                if (item.Key.StartsWith("Tag") && item.Value.Equals(term, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        // GET: Blob
        public ActionResult Index(string id)
        {
            // Pass a list of blob URIs and captions in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("pixaltospeak_AzureStorageConnectionString"));
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            List<BlobInfo> blobs = new List<BlobInfo>();
            

            foreach (IListBlobItem item in container.ListBlobs())
            {
                var blob = item as CloudBlockBlob;

                if (blob != null)
                {
                    blob.FetchAttributes(); // Get blob metadata
                    
                    if (String.IsNullOrEmpty(id) || HasMatchingMetadata(blob, id))
                    {
                        var caption = blob.Metadata.ContainsKey("Caption") ? blob.Metadata["Caption"] : blob.Name;
                        var height = blob.Metadata.ContainsKey("Height") ? blob.Metadata["Height"] : blob.Name;
                        var width = blob.Metadata.ContainsKey("Width") ? blob.Metadata["Width"] : blob.Name;
                        var format = blob.Metadata.ContainsKey("Format") ? blob.Metadata["Format"] : blob.Name;
                        var blackWhite = blob.Metadata.ContainsKey("BlackWhite") ? blob.Metadata["BlackWhite"] : blob.Name;
                        var foregroundColor = blob.Metadata.ContainsKey("ForegroundColor") ? blob.Metadata["ForegroundColor"] : blob.Name;
                        var backgroundColor = blob.Metadata.ContainsKey("BackgroundColor") ? blob.Metadata["BackgroundColor"] : blob.Name;
                        var colors = (blob.Metadata.ContainsKey("DominantColor1") ? blob.Metadata["DominantColor1"] : blob.Name);

                        var tags = (blob.Metadata.ContainsKey("Tag1") ? blob.Metadata["Tag1"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag2") ? blob.Metadata["Tag2"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag3") ? blob.Metadata["Tag3"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag4") ? blob.Metadata["Tag4"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag5") ? blob.Metadata["Tag5"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag6") ? blob.Metadata["Tag6"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag7") ? blob.Metadata["Tag7"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag8") ? blob.Metadata["Tag8"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag9") ? blob.Metadata["Tag9"] : blob.Name) + ", ";
                        tags += (blob.Metadata.ContainsKey("Tag10") ? blob.Metadata["Tag10"] : blob.Name);

                        blobs.Add(new BlobInfo()
                        {
                            ImageUri = blob.Uri.ToString(),
                            ThumbnailUri = blob.Uri.ToString().Replace("/photos/", "/thumbnails/"),
                            //Time = blob.Properties.LastModified.ToString(),
                            Caption = caption,
                            Height = height,
                            Width = width,
                            Format = format,
                            BlackWhite = blackWhite,
                            ForegroundColor = foregroundColor,
                            BackgroundColor = backgroundColor,
                            Colors = colors,
                            Tags = tags
                        });
                    }
                }
            }
            
            ViewBag.Blobs = blobs.ToArray();
            ViewBag.Search = id; // Prevent search box from losing its content
            return View();
        }
        
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                // Make sure the user selected an image file
                if (!file.ContentType.StartsWith("image"))
                {
                    TempData["Message"] = "Only image files may be uploaded";
                }
                else
                {
                    // Save the original image in the "photos" container
                    CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("pixaltospeak_AzureStorageConnectionString"));
                    CloudBlobClient client = account.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("photos");
                    CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                    await photo.UploadFromStreamAsync(file.InputStream);
                    file.InputStream.Seek(0L, SeekOrigin.Begin);

                    // Generate a thumbnail and save it in the "thumbnails" container
                    using (var outputStream = new MemoryStream())
                    {
                        var settings = new ResizeSettings { MaxWidth = 192, Format = "png" };
                        ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                        outputStream.Seek(0L, SeekOrigin.Begin);
                        container = client.GetContainerReference("thumbnails");
                        CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await thumbnail.UploadFromStreamAsync(outputStream);
                    }

                    // Submit the image to Azure's Computer Vision API
                    VisionServiceClient vision = new VisionServiceClient(
                        CloudConfigurationManager.GetSetting("SubscriptionKey"),
                        CloudConfigurationManager.GetSetting("VisionEndpoint")
                    );

                    VisualFeature[] features = new VisualFeature[] { VisualFeature.Description, VisualFeature.Color, VisualFeature.ImageType, VisualFeature.Tags, VisualFeature.Adult, VisualFeature.Categories };
                    var result = await vision.AnalyzeImageAsync(photo.Uri.ToString(), features);
                    
                    // Record the image description and tags in blob metadata
                    photo.Metadata.Add("Caption", result.Description.Captions[0].Text);
                    photo.Metadata.Add("Height", result.Metadata.Height.ToString());
                    photo.Metadata.Add("Width", result.Metadata.Width.ToString());
                    photo.Metadata.Add("Format", result.Metadata.Format.ToString());
                    photo.Metadata.Add("BlackWhite", result.Color.IsBWImg.ToString());
                    photo.Metadata.Add("ForegroundColor", result.Color.DominantColorForeground.ToString());
                    photo.Metadata.Add("BackgroundColor", result.Color.DominantColorBackground.ToString());

                    for (int i = 1; i < result.Color.DominantColors.Length; i++)
                    {
                        string key = String.Format("DominantColor{0}", i);
                        photo.Metadata.Add(key, result.Color.DominantColors[i]);
                    }

                    for (int i = 1; i < result.Description.Tags.Length; i++)
                    {
                        string key = String.Format("Tag{0}", i);
                        photo.Metadata.Add(key, result.Description.Tags[i]);
                    }

                    await photo.SetMetadataAsync();
                    
                }
            }
            
            // redirect back to the index action to show the form once again
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Search(string term)
        {
            return RedirectToAction("Index", new { id = term });
        }
    }
}