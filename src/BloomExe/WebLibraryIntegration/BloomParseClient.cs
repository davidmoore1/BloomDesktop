﻿using System;
using System.Collections.Generic;
using System.Net;
using Bloom.Book;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Deserializers;

namespace Bloom.WebLibraryIntegration
{
	public class BloomParseClient
	{
		private string kBaseUrl="https://api.parse.com/1/";
		private readonly RestClient _client;
		private string _sessionToken;
		private string _userId;

		public BloomParseClient()
		{
			_sessionToken = String.Empty;
			 _client = new RestClient(kBaseUrl);
		}

		// REST key. Unit tests update these.
		public string ApiKey = KeyManager.ParseApiKey;
		public string ApplicationKey = KeyManager.ParseApplicationKey;

		public string UserId {get { return _userId; }}

		public string Account { get; private set; }

		public bool LoggedIn
		{
			get
			{
				return !string.IsNullOrEmpty(_sessionToken);
			}
		}

		private RestRequest MakeRequest(string path, Method requestType)
		{
			// client.Authenticator = new HttpBasicAuthenticator(username, password);
			var request = new RestRequest(path, requestType);
			SetCommonHeaders(request);
			if (!string.IsNullOrEmpty(_sessionToken))
				request.AddHeader("X-Parse-Session-Token", _sessionToken);
			return request;
		}


		private RestRequest MakeGetRequest(string path)
		{
			return MakeRequest(path, Method.GET);
		}

		private void SetCommonHeaders(RestRequest request)
		{
			request.AddHeader("X-Parse-Application-Id", ApplicationKey);
			request.AddHeader("X-Parse-REST-API-Key", ApiKey);
		}

		private RestRequest MakePostRequest(string path)
		{
			return MakeRequest(path, Method.POST);
		}

		private RestRequest MakePutRequest(string path)
		{
			return MakeRequest(path, Method.PUT);
		}
		private RestRequest MakeDeleteRequest(string path)
		{
			return MakeRequest(path, Method.DELETE);
		}

		public int GetBookCount()
		{
			var request = MakeGetRequest("classes/books");
			request.AddParameter("count", "1");
			request.AddParameter("limit", "0");
			var response = _client.Execute(request);
			var dy = JsonConvert.DeserializeObject<dynamic>(response.Content);
			return dy.count;
		}

		public IRestResponse GetBookRecords(string query)
		{
			var request = MakeGetRequest("classes/books");
			request.AddParameter("where",query, ParameterType.QueryString);
			return _client.Execute(request);
		}

		public dynamic GetSingleBookRecord(string id, string uploader)
		{
			var json = GetBookRecords(id, uploader);
			if (json == null || json.Count < 1)
				return null;

			return json[0];
		}

		internal dynamic GetBookRecords(string id, string uploader)
		{
			var response = GetBookRecords("{\"bookInstanceId\":\"" + id + "\",\"uploadedBy\":\"" + uploader + "\"}");
			if (response.StatusCode != HttpStatusCode.OK)
				return null;
			dynamic json = JObject.Parse(response.Content);
			if (json == null)
				return null;
			return json.results;
		}


		public bool LogIn(string account, string password)
		{
			_sessionToken = String.Empty;
			Account = string.Empty;
			var request = MakeGetRequest("login");
			request.AddParameter("username", account);
			request.AddParameter("password", password);
			var response = _client.Execute(request);
			var dy = JsonConvert.DeserializeObject<dynamic>(response.Content);
			_sessionToken = dy.sessionToken;//there's also an "error" in there if it fails, but a null sessionToken tells us all we need to know
			_userId = dy.objectId;
			Account = account;
			return LoggedIn;
		}

		public IRestResponse CreateBookRecord(string metadataJson)
		{
			if (!LoggedIn)
				throw new ApplicationException();
			var request = MakePostRequest("classes/books");
			request.AddParameter("application/json", metadataJson, ParameterType.RequestBody);
			var response = _client.Execute(request);
			if(response.StatusCode!=HttpStatusCode.Created)
				throw new ApplicationException(response.StatusDescription+" "+response.Content);
			return response;
		}

		public IRestResponse SetBookRecord(string metadataJson)
		{
			if (!LoggedIn)
				throw new ApplicationException();
			var metadata = BookMetaData.FromString(metadataJson);
			var book = GetSingleBookRecord(metadata.Id, metadata.UploadedBy);
			if (book == null)
				return CreateBookRecord(metadataJson);

			var request = MakePutRequest("classes/books/" + book.objectId);
			request.AddParameter("application/json", metadataJson, ParameterType.RequestBody);
			var response = _client.Execute(request);
			if (response.StatusCode != HttpStatusCode.OK)
				throw new ApplicationException(response.StatusDescription + " " + response.Content);
			return response;
		}

		public void CreateUser(string account, string password)
		{
			var request = MakePostRequest("users");
			var metadataJson =
				"{\"username\":\"" + account + "\",\"password\":\"" + password + "\",\"email\":\"" + account + "\"}";
			request.AddParameter("application/json", metadataJson, ParameterType.RequestBody);
			var response = _client.Execute(request);
			if (response.StatusCode != HttpStatusCode.Created)
				throw new ApplicationException(response.StatusDescription + " " + response.Content);
		}

		public void DeleteCurrentUser()
		{
			if (!LoggedIn)
				throw new ApplicationException("Must be logged in to delete current user");
			var request = MakeDeleteRequest("users/" + _userId);
			var response = _client.Execute(request);
			if (response.StatusCode != HttpStatusCode.OK)
				throw new ApplicationException(response.StatusDescription + " " + response.Content);
			_sessionToken = null;
			_userId = null;
		}
	}
}