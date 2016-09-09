﻿using System;
using System.Linq;
using System.Text;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using System.Net;
//using System.Net.Security;
//using System.Security.Cryptography.X509Certificates;

using System.Threading;

using SimpleJSON;


namespace KerbalX
{
	public class KerbalXAPI
	{
		public static string token = null; 
		public static string json = "";

		//make request to site to authenticate token 
		public static void authenticate_token(string new_token){
			KerbalX.notify("Authenticating with KerbalX.com...");
			NameValueCollection queries = new NameValueCollection ();
			queries.Add ("token", new_token);
			KerbalXAPI.post ("http://localhost:3000/api/authenticate", queries, (resp, code) => {
				if(code==200){
					token = new_token;
					KerbalX.show_login = false;
					KerbalX.notify("You are logged in.");
				}else{
					KerbalX.show_login = true;
					KerbalX.notify("Enter your KerbalX username and password");
				}
			});
		}

		public static void login(string username, string password){
			//make request to site to authenticate username and password and get token back
			KerbalX.notify("loging into KerbalX.com...");
			NameValueCollection queries = new NameValueCollection ();
			queries.Add ("username", username);
			queries.Add ("password", password);
			KerbalXAPI.post ("http://localhost:3000/api/login", queries, (resp, code) => {
				if(code==200){
					var data = JSON.Parse (resp);
					token = data["token"];
					KerbalX.save_token (data["token"]);
					KerbalX.show_login = false;
					KerbalX.notify("login succsessful! KerbalX.key saved in KSP root");
				}else{
					KerbalX.show_login = true;
					KerbalX.alert = "login failed, check yo shit.";
					KerbalX.notice = "";
				}
				KerbalXLoginWindow.enable_login = true;
			});
		}

		//define delegate to be used to pass lambda statement as a callback to get, post and request methods.
		public delegate void RequestCallback(string data, int status_code);

		//Perform simple GET request 
		// Usage:
		//	KerbalXAPI.get ("http://some_website.com/path/to/stuff", (resp, code) => {
		//		//actions to perform after request completes. code provides the status code int and resp provides the returned string
		//	});
		public static void get(string url, RequestCallback callback){
			request ("GET", url, new NameValueCollection(), callback);
		}	

		//Perform GET request with query 
		// Usage:
		// NameValueCollection query = new NameValueCollection ();
		// query.Add ("username", "foobar");
		//	KerbalXAPI.get ("http://some_website.com/path/to/stuff", query, (resp, code) => {
		//		//actions to perform after request completes. code provides the status code int and resp provides the returned string
		//	});	
		public static void get(string url, NameValueCollection query, RequestCallback callback){
			request ("GET", url, query, callback);
		}

		//Perform POST request
		// Usage:
		// NameValueCollection query = new NameValueCollection ();
		// query.Add ("username", "foobar");
		//	KerbalXAPI.post ("http://some_website.com/path/to/stuff", query, (resp, code) => {
		//		//actions to perform after request completes. code provides the status code int and resp provides the returned string
		//	});	
		public static void post(string url, NameValueCollection query, RequestCallback callback){
			request ("POST", url, query, callback);
		}

		// Performs HTTP GET and POST requests - takes a method ('GET' or 'POST'), a url, query args and a callback delegate
		// The request is performed in a thread to facilitate asynchronous handling
		// Usage:
		// NameValueCollection query = new NameValueCollection ();
		// query.Add ("username", "foobar");
		//	KerbalXAPI.request ("GET", "http://some_website.com/path/to/stuff", query, (resp, code) => {
		//		//actions to perform after request completes. code provides the status code int and resp provides the returned string
		//	});	
		// OR
		//	KerbalXAPI.request ("POST", "http://some_website.com/path/to/stuff", query, (resp, code) => {
		//		//actions to perform after request completes. code provides the status code int and resp provides the returned string
		//	});	
		public static void request(string method, string url, NameValueCollection query, RequestCallback callback)
		{
			string response_data = null;
			int status_code = 500;

			var thread = new Thread (() => {
				try{
					KerbalX.log("sending request to: " + url);
					//ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
					var client = new WebClient();
					client.Headers.Add("token", token);
					//client.Headers.Add(HttpRequestHeader.UserAgent, "User-Agent: Mozilla/5.0 (Windows NT 6.1; WOW64; rv:8.0) Gecko/20100101 Firefox/8.0");
					if(method == "GET"){
						client.QueryString = query;	
						response_data = client.DownloadString (url);
					}else if(method == "POST"){
						response_data = Encoding.Default.GetString(client.UploadValues (url, "POST", query));
					}
					status_code = 200;
				}
				catch(WebException e){
					HttpWebResponse resp = (System.Net.HttpWebResponse)e.Response;
					KerbalX.log ("request failed with " + resp.StatusCode + "-" + (int)resp.StatusCode);
					status_code = (int)resp.StatusCode;
				}
				catch (Exception e){
					KerbalX.log ("unhandled exception in request: ");			
					KerbalX.log (e.Message);
					status_code = 500;
				}

				callback(response_data, status_code); //call the callback method and pass in the response and status code.
			});
			thread.Start ();
		}
	}

}
