﻿// /*
//  Copyright (c) Stefan Gordon
//  All Rights Reserved
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the
//  License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
//  THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
//  INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
//  MERCHANTABLITY OR NON-INFRINGEMENT.
// 
//  See the Apache 2 License for the specific language governing permissions and limitations under the License.
//  */

#region

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AzureEncryptionExtensions;
using AzureEncryptionExtensions.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

#endregion

namespace AzureBlobEncryptionTests
{
    /// <summary>
    /// You'll need to run the Azure Storage Emulator for these tests to pass.
    /// </summary>
    [TestClass]
    public class FunctionalTests
    {

        [TestMethod]
        public void BlockBlob_UploadDownload_File()
        {

            using (var file = new TemporaryFile(512))
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobClient.GetContainerReference("testcontainer");

                container.CreateIfNotExists();

                CloudBlockBlob blob = container.GetBlockBlobReference(file.fileInfo.Name);

                // Create provider
                var provider = new SymmetricBlobCryptoProvider();

                // Upload file
                blob.UploadFromFileEncrypted(provider, file.fileInfo.FullName, FileMode.Open);

                // Download file
                string destinationFile = file.fileInfo.FullName + "decrypted";
                blob.DownloadToFileEncrypted(provider, destinationFile, FileMode.Create);

                // Compare raw and decrypted files
                Assert.AreEqual(GetFileHash(file.fileInfo.FullName), GetFileHash(destinationFile));

                // Download file again, without our library, to ensure it was actually encrypted
                string encryptedDestinationFile = file.fileInfo.FullName + "encrypted";
                blob.DownloadToFile(encryptedDestinationFile, FileMode.Create);

                // Delete blob
                blob.DeleteIfExists();

                // Compare raw and encrypted files
                Assert.AreNotEqual(GetFileHash(file.fileInfo.FullName), GetFileHash(encryptedDestinationFile));

                // Cleanup
                if (File.Exists(destinationFile))
                {
                    File.Delete(destinationFile);
                }

                if (File.Exists(encryptedDestinationFile))
                {
                    File.Delete(encryptedDestinationFile);
                }
            }
        }

        [TestMethod]
        public void BlockBlob_UploadDownload_Stream()
        {
            // Prepare random memory stream
            Random random = new Random();
            byte[] buffer = new byte[512];
            random.NextBytes(buffer);
            MemoryStream testStream = new MemoryStream(buffer);

            // Get a blob reference
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("testcontainer");
            container.CreateIfNotExists();
            CloudBlockBlob blob = container.GetBlockBlobReference(Guid.NewGuid().ToString());

            // Create provider
            var provider = new SymmetricBlobCryptoProvider();

            // Upload stream
            blob.UploadFromStreamEncrypted(provider, testStream);

            // Download stream
            MemoryStream downloadedStream = new MemoryStream();
            blob.DownloadToStreamEncrypted(provider, downloadedStream);

            // Compare raw and decrypted streams
            Assert.IsTrue(testStream.ToArray().SequenceEqual(downloadedStream.ToArray()));

            // Download file again, without our library, to ensure it was actually encrypted
            MemoryStream encryptedStream = new MemoryStream();
            blob.DownloadToStream(encryptedStream);

            // Delete blob
            blob.DeleteIfExists();

            // Compare raw and encrypted streams
            Assert.IsFalse(testStream.ToArray().SequenceEqual(encryptedStream.ToArray()));
        }

        [TestMethod]
        [DeploymentItem("TestCertificates")]
        public void BlockBlob_UploadDownload_Stream_Asymmetric()
        {
            // Prepare random memory stream
            Random random = new Random();
            byte[] buffer = new byte[512];
            random.NextBytes(buffer);
            MemoryStream testStream = new MemoryStream(buffer);

            // Get a blob reference
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("testcontainer");
            container.CreateIfNotExists();
            CloudBlockBlob blob = container.GetBlockBlobReference(Guid.NewGuid().ToString());

            // Create provider
            X509Certificate2 cert = new X509Certificate2("4096.pfx", string.Empty, X509KeyStorageFlags.Exportable);
            var provider = new AsymmetricBlobCryptoProvider(cert, true);

            // Upload stream
            blob.UploadFromStreamEncrypted(provider, testStream);

            // Download stream
            MemoryStream downloadedStream = new MemoryStream();
            blob.DownloadToStreamEncrypted(provider, downloadedStream);

            // Compare raw and decrypted streams
            Assert.IsTrue(testStream.ToArray().SequenceEqual(downloadedStream.ToArray()));

            // Download file again, without our library, to ensure it was actually encrypted
            MemoryStream encryptedStream = new MemoryStream();
            blob.DownloadToStream(encryptedStream);

            // Delete blob
            blob.DeleteIfExists();

            // Compare raw and encrypted streams
            Assert.IsFalse(testStream.ToArray().SequenceEqual(encryptedStream.ToArray()));
        }

        /// <summary>
        /// Upload using UploadFromFileEncrypted
        /// but download using DownloadToStreamEncrypted
        /// </summary>
        [TestMethod]
        public void BlockBlob_UploadDownload_File_Stream()
        {

            using (var file = new TemporaryFile(512))
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobClient.GetContainerReference("testcontainer");

                container.CreateIfNotExists();

                CloudBlockBlob blob = container.GetBlockBlobReference(file.fileInfo.Name);

                // Create provider
                var provider = new SymmetricBlobCryptoProvider();

                // Upload file
                blob.UploadFromFileEncrypted(provider, file.fileInfo.FullName, FileMode.Open);

                // Download stream
                MemoryStream downloadedStream = new MemoryStream();
                blob.DownloadToStreamEncrypted(provider, downloadedStream);

                // Compare raw and decrypted data
                Assert.AreEqual(GetFileHash(file.fileInfo.FullName), GetStreamHash(downloadedStream));

                // Download file again, without our library, to ensure it was actually encrypted
                MemoryStream encryptedStream = new MemoryStream();
                blob.DownloadToStream(encryptedStream);

                // Delete blob
                blob.DeleteIfExists();
            }
        }

        public string GetFileHash(string filename)
        {
            var hash = new SHA1Managed();
            var clearBytes = File.ReadAllBytes(filename);
            var hashedBytes = hash.ComputeHash(clearBytes);
            return ConvertBytesToHex(hashedBytes);
        }

        public string GetStreamHash(MemoryStream data)
        {
            var hash = new SHA1Managed();
            var clearBytes = data.ToArray();
            var hashedBytes = hash.ComputeHash(clearBytes);
            return ConvertBytesToHex(hashedBytes);
        }

        public string ConvertBytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x"));
            }
            return sb.ToString();
        }

    }
}
