﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telestream.Cloud.SDK.Core;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Telestream.Cloud.SDK.Model;
using System.IO;
using System.Net.Http;
namespace Telestream.Cloud.SDK
{
	public class TelestreamCloudService : ServiceBase
	{
		public TelestreamCloudService()
			: this(TelestreamCloudConfig.Credentials)
		{

		}

		public TelestreamCloudService(ApiAccess apiAccess)
			: base(apiAccess)
		{
		}

		public TelestreamCloudService(IConfigurationService configuration)
			: base(configuration)
		{
		}

		public Task<List<Factory>> GetFactories()
		{
			var request = _requestFactory.Get("factories.json");
			return _client.Invoke<List<Factory>>(request);
		}

		public Task DeleteVideo(string factoryId, string videoId)
		{
			return InvokeDelete(factoryId, string.Format("videos/{0}.json", videoId));
		}

		public Task DeleteVideoSource(string factoryId, string videoId)
		{
			return InvokeDelete(factoryId, string.Format("videos/{0}/source.json", videoId));
		}

		public Task DeleteEncoding(string factoryId, string encodingId)
		{
			ValidateId("encodingId", encodingId);

			return InvokeDelete(factoryId, string.Format("encodings/{0}.json", encodingId));
		}

		public Task DeleteProfile(string factoryId, string profileId)
		{
			ValidateId("profileId", profileId);

			return InvokeDelete(factoryId, string.Format("profiles/{0}.json", profileId));
		}

		public Task<List<Video>> GetVideos(string factoryId)
		{
			ValidateFactoryId(factoryId);

			return InvokeGet<List<Video>>(factoryId, "videos.json");
		}

		public async Task<Video> GetVideo(string factoryId, string videoId, bool fetchEncodings = false)
		{
			ValidateVideoId(videoId);

			var video = await InvokeGet<Video>(
						factoryId,
						string.Format("videos/{0}.json", videoId));

			if (fetchEncodings)
			{
				var encodings = await GetEncodings(factoryId, video.Id);
				video.Encodings = encodings;
			}

			return video;
		}

		public Task<List<VideoEncoding>> GetEncodings(string factoryId, EncodingStatus status = null, string profileId = null, string profileName = null, string videoId = null, bool? screenshots = null, int? page = null, int? perPage = null)
		{
			return InvokeGet<List<VideoEncoding>>(
					factoryId,
					"encodings.json",
					QueryParamList.New()
						.AddNonEmpty("status", status)
						.AddNonEmpty("profile_id", profileId)
						.AddNonEmpty("profile_name", profileName)
						.AddNonEmpty("video_id", videoId)
						.AddNonEmpty("screenshots", screenshots)
						.AddNonEmpty("page", page)
						.AddNonEmpty("per_page", perPage));
		}

		public Task<List<VideoEncoding>> GetEncodings(string factoryId, string videoId)
		{
			ValidateVideoId(videoId);

			return InvokeGet<List<VideoEncoding>>(
					factoryId,
					string.Format("videos/{0}/encodings.json", videoId));
		}

		public Task<VideoEncoding> GetEncoding(string factoryId, string encodingId, bool? screenshots = null)
		{
			ValidateId("encodingId", encodingId);

			return InvokeGet<VideoEncoding>(
				factoryId,
				string.Format("encodings/{0}.json", encodingId),
				QueryParamList.New()
					.AddNonEmpty("screenshots", screenshots));
		}

		public Task<VideoEncoding> CreateEncoding(string factoryId, string videoId, string profileId, string profileName)
		{
			ValidateId("videoId", videoId);

			return InvokePost<VideoEncoding>(
				factoryId,
				"encodings.json",
				QueryParamList.New()
				.AddNonEmpty("video_id", videoId)
				.AddNonEmpty("profile_id", profileId)
				.AddNonEmpty("profileName", profileName));
		}

		public Task<VideoEncoding> CancelEncoding(string factoryId, string encodingId)
		{
			ValidateId("encodingId", encodingId);

			return InvokePost<VideoEncoding>(
				factoryId,
				string.Format("encodings/{0}/cancel.json", encodingId),
				null);
		}

		public Task<VideoEncoding> RetryEncoding(string factoryId, string encodingId)
		{
			ValidateId("encodingId", encodingId);

			return InvokePost<VideoEncoding>(
				factoryId,
				string.Format("encodings/{0}/retry.json", encodingId),
				null);
		}

		public Task<List<VideoProfile>> GetProfiles(string factoryId, bool? expand = null, int? page = null, int? perPage = null)
		{
			return InvokeGet<List<VideoProfile>>(
				factoryId,
				"profiles.json",
				QueryParamList.New()
					.AddNonEmpty("expand", expand)
					.AddNonEmpty("page", page)
					.AddNonEmpty("per_page", perPage));
		}

		public Task<VideoProfile> GetProfile(string factoryId, string idOrName, bool? expand = null)
		{
			ValidateId("idOrName", idOrName);

			return InvokeGet<VideoProfile>(
				factoryId,
				string.Format("profiles/{0}.json", idOrName),
				QueryParamList.New().AddNonEmpty("expand", expand));
		}

		public Task<VideoProfile> CreateProfile(string factoryId, VideoProfile profile)
		{
			if (profile == null) { throw new ArgumentNullException("profile"); }

			return InvokePost<VideoProfile>(factoryId, "profiles.json", profile);
		}

		public Task<VideoProfile> UpdateProfile(string factoryId, VideoProfile profile)
		{
			if (profile == null) { throw new ArgumentNullException("profile"); }

			return InvokePut<VideoProfile>(factoryId, string.Format("profiles/{0}.json", profile.Id), profile);

		}

		public Task<VideoMetadata> Metadata(string factoryId, string videoId)
		{
			ValidateVideoId(videoId);

			return InvokeGet<VideoMetadata>(factoryId, string.Format("videos/{0}/metadata.json", videoId));
		}

		public Task<Factory> ChangeFactoryName(string factoryId, string newName)
		{
			ValidateFactoryId(factoryId);
			var request = _requestFactory.PutJson(string.Format("factories/{0}.json", factoryId), new { name = newName }, null);
			return _client.Invoke<Factory>(request);
		}

		public Task<UploadSession> StartUpload(long fileSize, string fileName)
		{
			const string FILE_SIZE = "file_size";
			const string FILE_NAME = "file_name";
			const string PROFILES = "profiles";

			var request = _requestFactory.Post("videos/upload.json",
				new QueryParamList()
				.Add(FILE_SIZE, fileSize.ToString())
				.Add(FILE_NAME, fileName)
				.Add(PROFILES, "h264"), null);

			return _client.Invoke<UploadSession>(request);
		}

		public async Task UploadFile(UploadSession session, Stream dataStream, IProgress<double> progress)
		{
			int chunkSize = 5 * 1024 * 1024;
			long pos = 0;
			byte[] buffer = new byte[chunkSize];
			HttpClient client = new HttpClient();


			dataStream.Seek(pos, SeekOrigin.Begin);

			int readed = 0;
			while ((readed = dataStream.Read(buffer, 0, buffer.Length)) != 0)
			{

				System.Diagnostics.Debug.WriteLine("Content-Lenght:{0}", chunkSize.ToString());
				System.Diagnostics.Debug.WriteLine("Content-Range: {0}", string.Format("bytes {0}-{1}/{2}", pos, dataStream.Position - 1, dataStream.Length));

				HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, session.Location);
				message.Headers.Add("Cache-Control", "no-cache");
				message.Content = new ByteArrayContent(buffer);
				message.Content.Headers.Add("Content-Range", string.Format("bytes {0}-{1}/{2}", pos, dataStream.Position - 1, dataStream.Length));
				message.Content.Headers.Add("Content-Type", "application/octet-stream");
				var resp = await client.SendAsync(message);
				var aa = await resp.Content.ReadAsStringAsync();

				pos = dataStream.Position;
				if (progress != null)
				{
					progress.Report((pos / (double)dataStream.Length) * 100);
				}
			}

			client = new HttpClient();
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/octet-stream");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Range", "bytes */" + dataStream.Length);
			client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Lenght", "0");
		}
	}
}