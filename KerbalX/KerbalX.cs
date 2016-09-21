﻿using System;
using System.Linq;
using System.Text;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

using System.IO;
using System.Threading;

using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;



namespace KerbalX
{
	public class KerbalX
	{
		public static string token_path = Path.Combine (KSPUtil.ApplicationRootPath, "KerbalX.key");
		public static List<string> log_data = new List<string>();
		public static string notice = "";
		public static string alert = "";

		public static string site_url = "http://localhost:3000";

		public static string screenshot_dir = Paths.joined (KSPUtil.ApplicationRootPath, "Screenshots"); //TODO make this a setting, oh and make settings.

		public static Dictionary<int, Dictionary<string, string>> existing_craft; //container for listing of user's craft already on KX and some details about them.

		//window handles (cos a window without a handle is just a pane)
		public static KerbalXConsole console 				= null;
		public static KerbalXLoginWindow login_gui 			= null;
		public static KerbalXEditorWindow editor_gui 		= null;
		public static KerbalXImageSelector image_selector 	= null;


		//methodical things
		//takes partial url and returns full url to site; ie url_to("some/place") -> "http://whatever_domain_site_url_defines.com/some/place"
		public static string url_to (string path){
			if(!path.StartsWith ("/")){ path = "/" + path;}
			return site_url + path;
		}

		//logging stuf, not suitable for lumberjacks
		public static void log (string s){ 
			s = "[KerbalX] " + s;
			log_data.Add (s); 
			Debug.Log (s);
		}
		public static string last_log()
		{
			if(log_data.Count != 0){
				return log_data [log_data.Count - 1];
			}else{
				return "nothing logged yet";
			}
		}
		public static void show_log(){
			foreach (string l in log_data) { Debug.Log (l); }
		}
		public static void notify(string s){
			notice = s;
			log (s);
		}


	}

	public delegate void DialogAction();
	public class KerbalXDialog : KerbalXWindow
	{
		public static KerbalXDialog instance;
		public float dialog_width = 300f;
		public string message = "";
		public DialogAction ok_action = null;
		public string ok_text = "OK";

		private void Start(){
			window_pos = new Rect((Screen.width/2 - dialog_width/2), Screen.height/4, dialog_width, 5);	
			window_title = "";
			footer = false;
			ok_action = () => {
				GameObject.Destroy (KerbalXDialog.instance);
			};
		}

		protected override void WindowContent(int win_id){
			GUILayout.Label (message);
			if(ok_action != null){
				if(GUILayout.Button (ok_text)){
					ok_action ();
				}
			}
		}
	}


	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class KerbalXLoginWindow : KerbalXWindow
	{
		private string username = "";
		private string password = "";
		public bool enable_login = true;  //used to toggle enabled/disabled state on login fields and button
		public bool show_login = false;
		public bool login_failed = false;
		public bool login_successful = false;


		GUIStyle alert_style = new GUIStyle();


		private void Start(){
			window_title = "KerbalX::Login";
			window_pos = new Rect((Screen.width/2 - 400/2),100, 400, 5);
			KerbalX.login_gui = this;
			alert_style.normal.textColor = Color.red;
			enable_request_handler ();

			//try to load a token from file and if present authenticate it with KerbalX.  if token isn't present or authentication fails the show login fields
			if (KerbalXAPI.token_not_loaded()) {
				KerbalXAPI.load_and_authenticate_token ();	
			}
		}

		protected override void WindowContent(int win_id)
		{
			if(show_login){					
				GUI.enabled = enable_login;
				GUILayout.Label ("Enter your KerbalX username and password");
				section (w => {
					GUILayout.Label ("username", GUILayout.Width (60f));
					username = GUILayout.TextField (username, 255, width (w-60f));
				});
				section (w => {
					GUILayout.Label ("password", GUILayout.Width(60f));
					password = GUILayout.PasswordField (password, '*', 255, width (w-60f));
				});
				GUI.enabled = true;
			}

			if (KerbalXAPI.token_loaded ()) {
				GUILayout.Label ("You are logged in");
			}
			if(login_successful){
				section (w => {
					GUILayout.Label ("KerbalX.key saved in KSP root", width (w-20f));
					if (GUILayout.Button ("?", width (20f))) {
						KerbalXDialog dialog = gameObject.AddOrGetComponent<KerbalXDialog> ();
						KerbalXDialog.instance = dialog;
						dialog.message = "The KerbalX.key is a token that is used to authenticate you with the site." +
							"\nIt will also persist your login, so next time you start KSP you won't need to login again." +
							"\nIf you want to login to KerbalX from multiple instances of KSP copy the KerbalX.key file into each install.";
					}
				});
			}

			GUI.enabled = enable_login;
			if (show_login) {
				if (GUILayout.Button ("Login")) {				
					KerbalX.alert = "";
					enable_login = false;
					login_failed = false;
					KerbalXAPI.login (username, password);
				}
			}else{
				if (GUILayout.Button ("Log out")) {
					show_login = true;
					KerbalXAPI.clear_token ();
					KerbalX.notify ("logged out");
				}				
			}
			GUI.enabled = true;

			if(login_failed){
				v_section (w => {
					GUILayout.Label ("Login failed, check your things", alert_style);
					if (GUILayout.Button ("Forgot your password? Go to KerbalX to reset it.")) {
						Application.OpenURL ("https://kerbalx.com/users/password/new");
					}
				});
			}
		}
	}


	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class KerbalXEditorWindow : KerbalXWindow
	{
		public string current_editor = null;
		private string craft_name = null;
		private string editor_craft_name = "";

		private string[] upload_errors = new string[0];
		private string mode = "upload";
		private float win_width = 410f;

		private bool first_pass = true;
		//private string image = "";

		private DropdownData craft_select;
		private DropdownData style_select;


		private Dictionary<int, string> remote_craft = new Dictionary<int, string> (); //will contain a mapping of KX database-ID to craft name
		List<int> matching_craft_ids = new List<int> ();	//will contain any matching craft names

		private Dictionary<int, string> craft_styles = new Dictionary<int, string> (){
			{0, "Ship"}, {1, "Aircraft"}, {2, "Spaceplane"}, {3, "Lander"}, {4, "Satellite"}, {5, "Station"}, {6, "Base"}, {7, "Probe"}, {8, "Rover"}, {9, "Lifter"}
		};

		public List<PicData> pictures = new List<PicData> ();

		GUIStyle alert_style 	= new GUIStyle();
		GUIStyle upload_button  = new GUIStyle();
		GUIStyle wrapped_button = new GUIStyle();
		GUIStyle centered 		= new GUIStyle();
		GUIStyle header_label	= new GUIStyle();

		//private Texture2D kx_logo = new Texture2D(56, 56, TextureFormat.ARGB32, false);
		//kx_logo = GameDatabase.Instance.GetTexture (Paths.joined ("KerbalX", "Assets", "KXlogo"), false);


		private void Start()
		{
			window_title = "KerbalX::Upload";
			window_pos = new Rect ((Screen.width - win_width - 100), 60, win_width, 5);
			prevent_editor_click_through = true;
			enable_request_handler ();
			KerbalX.editor_gui = this;
			KerbalXAPI.fetch_existing_craft (() => {  //Query KX for the user's current craft (which gets stashed on KerablX.existing_craft). lambda gets called once request completes.
				remote_craft.Clear ();
				remote_craft.Add (0, "select a craft");	//remote_craft populates the select menu, ID 0 (which can't exist on KX) is used as the placeholder
				foreach(KeyValuePair<int, Dictionary<string, string>> craft in KerbalX.existing_craft){
					remote_craft.Add (craft.Key, craft.Value["name"]);
				}
			});
		}

		private void set_stylz(){
			alert_style.normal.textColor = Color.red;
			upload_button = new GUIStyle (GUI.skin.button);
			upload_button.fontSize = 20;
			upload_button.padding = new RectOffset (3, 3, 10, 10);

			wrapped_button = new GUIStyle (GUI.skin.button);
			wrapped_button.wordWrap = true;

			centered = new GUIStyle (GUI.skin.label);
			centered.alignment = TextAnchor.UpperCenter;

			header_label = new GUIStyle (GUI.skin.label);
			header_label.fontSize = 15;
			header_label.fontStyle = FontStyle.Bold;
//			GUI.skin.label.fontSize = 20;
		}

		protected override void WindowContent(int win_id)
		{
			if (first_pass) {
				first_pass = false;
				set_stylz ();//it's like we need a sorta sheet of styles, maybe one that can cascade, a cascading style sheet if you will.
			}

			//get the craft name from the editor field, but allow the user to set a alternative name to upload as without changing the editor field
			//but if the user changes the editor field then reset the craft_name to that. make sense? good, shutup. 
			if(editor_craft_name != EditorLogic.fetch.ship.shipName){
				craft_name = EditorLogic.fetch.ship.shipName;	
				check_for_matching_craft_name ();
			}
			editor_craft_name = EditorLogic.fetch.ship.shipName;

			//Perform checks to see if there is a craft,  its not untitled and a craft file for it exists.
			string trimmed_lowered_name = editor_craft_name.Trim ().ToLower ().Replace (" ", "");
			if(part_info ().Count == 0){
				GUILayout.Label ("No craft loaded. Create or load a craft to continue.", header_label);
			}else if(trimmed_lowered_name == "untitledspacecraft" || trimmed_lowered_name == EditorLogic.autoShipName){
				GUILayout.Label (editor_craft_name + "? Really?\nHow about you name the poor thing before uploading!", header_label);
			}else if(!craft_file_exists ()){
				section (win_width, w => {
					GUILayout.Label ("This craft hasn't been saved yet\nNo craft file found for " + editor_craft_name, header_label, width(w*0.7f));
					if(GUILayout.Button ("Save it now", width(w*0.3f), height (40))){
						EditorLogic.fetch.saveBtn.onClick.Invoke ();
					}
				});
			}else{
				//if checks pass continue with drawing rest of interface (TODO check for unsaved changes).

				string mode_title = new CultureInfo ("en-US", false).TextInfo.ToTitleCase (mode);
				GUILayout.Label (mode_title + " '" + craft_name + "' " + (mode == "update" ? "on" : "to") + " KerbalX", header_label);
				
				if(mode == "upload"){
					section (w => {
						GUILayout.Label ("Enter details about your craft", width(w*0.45f));
						GUILayout.Label ("OR", centered, width(w*0.1f));
						if (GUILayout.Button ("Update An Existing Craft", width(w*0.45f))) {
							change_mode("update");
							if (matching_craft_ids.Count != 1) { craft_select.id = 0;};
						}
					});
					
					section (w => {
						string current_craft_name = craft_name; //used to detect if user changes craft_name field (GUI.changed gets triggered by above button)
						GUILayout.Label ("Craft name:", width(80f));
						craft_name = GUILayout.TextField (craft_name, 255, width(w - 80));
						if(craft_name != current_craft_name){check_for_matching_craft_name (); } //check for matching existing craft
					});
					
					section (w => {
						GUILayout.Label ("Select craft type:", width(100f));
						style_select = dropdown (craft_styles, style_select, 100f, 100f);
					});
					
					section (w => {
						if (GUILayout.Button ("Edit Action Group info", width(w*0.5f), height(30)) ) {
						}					
						if (GUILayout.Button ("Add Pictures", width(w*0.5f), height (30)) ) {
							KerbalX.image_selector.toggle ();
						}					
					});

					v_section (w => {
						section (w2 => {
							foreach(PicData pic in pictures){
								v_section (80f, w3 => {
									if(pic.file != null){
										GUILayout.Label (pic.texture, width (w3), height (w3*0.75f));
										if(GUILayout.Button ("remove")){
											pictures.Remove (pic);
											this.autoheight ();
										}
									}
								});
							}
						});
						foreach(PicData pic in pictures){
							section(w2 => {
								if(pic.url != null){
									GUILayout.Label (pic.url, width (w2-80f));
									if(GUILayout.Button ("remove", width (80f))){
										pictures.Remove (pic);
										this.autoheight ();
									}
								}
							});
						}
					});
					
				}else if(mode == "update"){
					if (matching_craft_ids.Count > 0) {
						string label_text = "This craft's name matches the name of " + (matching_craft_ids.Count == 1 ? "a" : "several") + " craft you've already uploaded.";
						if (matching_craft_ids.Count > 1) {
							label_text = label_text + " Select which one you want to update";
						}
						GUILayout.Label (label_text);
					}
					
					section (w => {
						v_section (w*0.7f, inner_w => {
							section (inner_w, inner_w2 => { GUILayout.Label ("Select Craft on KerbalX to update:"); });
							craft_select = dropdown (remote_craft, craft_select, inner_w, 100f);
						});
						v_section (w*0.3f, inner_w => {
							section (inner_w, inner_w2 => {
								if (GUILayout.Button ("OR upload this as a new craft", wrapped_button, height (50) )) {
									change_mode("upload");
								}
							});
						});
					});
					
					if (craft_select.id > 0) {
						GUILayout.Label ("id:" + craft_select.id + ", name:" + KerbalX.existing_craft [craft_select.id] ["name"] + " - " + KerbalX.existing_craft [craft_select.id] ["url"]);
					}
				}
				
				
				if (KerbalX.alert != "") {	
					GUILayout.Label (KerbalX.alert, alert_style, width (win_width) );
				}
				if (upload_errors.Count () > 0) {
					GUILayout.Label ("errors and shit");
					foreach (string error in upload_errors) {
						GUILayout.Label (error.Trim (), alert_style, width (win_width));
					}
				}
				
				style_override = new GUIStyle();
				style_override.padding = new RectOffset (20, 20, 10, 10);
				section (w => {
					if (GUILayout.Button (mode_title + "!", upload_button)) {
						upload_craft ();
					}
				});
			}


			if(GUILayout.Button ("test")){
//				EditorLogic.fetch.newBtn.onClick.AddListener (() => {
//					Debug.Log ("NEW CLICKED");
//				});
				//EditorLogic.fetch.newBtn.Select ();
				//EditorLogic.fetch.newBtn.onClick.Invoke ();
				//EditorLogic.fetch.saveBtn.onClick.Invoke ();

//				this.visible = false;
//				Application.CaptureScreenshot ("fibble");
//				this.visible = true;
				
				window_pos.width = window_pos.width + 10;


			}

		}

		//check if craft_name matches any of the user's existing craft.  Sets matching_craft_ids to contain KX database ids of any matching craft
		//if only one match is found then craft_select.id is also set to the matched id (which them selects the craft in the select menu)
		private void check_for_matching_craft_name(){
			KerbalX.log ("checking for matching craft - " + craft_name);
			string lower_name = craft_name.Trim ().ToLower ();
			matching_craft_ids.Clear ();
			foreach(KeyValuePair<int, string> craft in remote_craft){
				string rc_lower = craft.Value.Trim ().ToLower ();
				if( lower_name == rc_lower || lower_name == rc_lower.Replace ("-", " ")){
					matching_craft_ids.Add (craft.Key);
				}
			}
			change_mode (matching_craft_ids.Count > 0 ? "update" : "upload");
			if(matching_craft_ids.Count == 1){
				craft_select.id = matching_craft_ids.First ();
			}			
		}

		private void change_mode(string new_mode){
			mode = new_mode;
			autoheight ();
		}

		//returns the craft file
		private string craft_file(){
			//return EditorLogic.fetch.ship.SaveShip ().ToString ();
			return System.IO.File.ReadAllText(craft_path ());
		}

		private bool craft_file_exists(){
			return System.IO.File.Exists (craft_path ());
		}

		//returns the path of the craft file
		private string craft_path(){
			return ShipConstruction.GetSavePath (editor_craft_name);
			//string path = Paths.joined (KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder, "Ships", current_editor, editor_craft_name);
			//return path + ".craft";
		}

		//returns a unique set of the craft's parts and data about each part;
		//{"partname1" => {"mod" => "mod_name"}, "partname2" => {"mod" => "mod_name"}, ....} #yeah, explained in Ruby hash notation, cos...it's terse. 
		private Dictionary<string, object> part_info(){
			Dictionary<string, object> part_data = new Dictionary<string, object>();
			var part_list = EditorLogic.fetch.ship.parts;
			foreach(Part part in part_list){
				if (!part_data.ContainsKey (part.name)) {
					Dictionary<string, object> part_detail = new Dictionary<string, object>();
					part_detail.Add ("mod", part.partInfo.partUrl.Split ('/') [0]);
					//part.partInfo.partConfig
					part_data.Add (part.name, part_detail);
				}
			}
			return part_data;
		}

		private void upload_craft(){
			//Array.Clear (upload_errors, 0, upload_errors.Length);	//remove any previous upload errors
			upload_errors = new string[0];
			KerbalX.alert = "";

			NameValueCollection data = new NameValueCollection ();	//contruct upload data
			data.Add ("craft_file", craft_file());
			data.Add ("craft_name", craft_name);
			data.Add ("part_data", JSONX.toJSON (part_info ()));
			HTTP.post (KerbalX.url_to ("api/craft.json"), data).set_header ("token", KerbalXAPI.temp_view_token ()).send ((resp, code) => {
				
				string message = "";
				if(code == 200){
					var resp_data = JSON.Parse (resp);
					KerbalX.log ("holy fuck! it uploaded");
				
				}else if(code == 422){
					var resp_data = JSON.Parse (resp);
					KerbalX.log ("upload failed");
					KerbalX.log (resp);
					string resp_errs = resp_data["errors"];
					upload_errors = resp_errs.Split (',');
					KerbalX.alert = "Craft Failed to Upload";

				}else{
					message = "upload failed - server error";
					KerbalX.alert = message;
					KerbalX.log (message);
				}
			});
		}
	}


	public struct PicData{
		public string name;
		public string url;
		public FileInfo file;
		public Texture2D texture;
		public void initialize(string new_name, FileInfo new_file, Texture2D new_texture){
			name = new_name;
			file = new_file;
			texture = new_texture;
		}
	}
	
	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class KerbalXImageSelector : KerbalXWindow
	{
		private List<PicData> pictures = new List<PicData>();				//populated by load_pics, contains PicData objects for each pic 
		private List<List<PicData>> groups = new List<List<PicData>> ();	//nested list of lists - rows of pictures for display in the interface

		private string mode = "pic_selector";
		private int pics_per_row = 4;
		private string[] file_types = new string[]{"jpg", "png"};
		private Vector2 scroll_pos;
		private int file_count = 0;

		GUIStyle pic_link 	 = new GUIStyle();
		GUIStyle pic_hover	 = new GUIStyle();
		GUIStyle header_label= new GUIStyle();

		Texture2D pic_highlight 	= new Texture2D(1, 1, TextureFormat.RGBA32, false);
		Texture2D scroll_background = new Texture2D(1, 1, TextureFormat.RGBA32, false);


		private string pic_url = "";
		private string hover_ele = "";

		private void Start(){
			window_title = "KerbalX::ScreenShots";
			float w = 610;
			window_pos = new Rect((Screen.width/2 - w/2), Screen.height/4, w, 5);
			visible = false;
			prevent_editor_click_through = true;
			KerbalX.image_selector = this;
			
			pic_highlight.SetPixel(0, 0, new Color (0.4f,0.5f,0.9f,1));
			pic_highlight.Apply ();
			
			scroll_background.SetPixel(0, 0, new Color (0.12f,0.12f,0.12f,0.7f));
			scroll_background.Apply ();
		}

		protected override void on_show(){
			change_mode ("pic_selector");
			pic_url = "";
			int count = picture_files ().Count;
			if(count != file_count){
				prepare_pics ();
			}
		}

		protected override void WindowContent(int win_id)
		{
			GUILayout.Label (GUI.skin.name);

			pic_link = new GUIStyle (GUI.skin.label);
			pic_link.padding = new RectOffset (5, 5, 5, 5);
			pic_link.margin = new RectOffset (0, 0, 0, 0);

			pic_hover = new GUIStyle (pic_link);
			pic_hover.normal.background = pic_highlight;
			pic_hover.normal.textColor = Color.black;

			header_label = new GUIStyle (GUI.skin.label);
			header_label.fontSize = 15;
			header_label.fontStyle = FontStyle.Bold;

			if(mode == "url_entry"){
				v_section (w => {
					GUILayout.Label ("Enter the URL to your image", header_label);
					GUILayout.Label ("note: one of 'em urls what end with an extension ie .jpg");
					section(w2 => {
						pic_url = GUILayout.TextField (pic_url, width (w2-100f));
						if(GUILayout.Button ("Add url", width (100f))){
							PicData pic = new PicData();
							pic.url = pic_url;
							KerbalX.editor_gui.pictures.Add (pic);
							this.hide ();
						};	
					});
					if(GUILayout.Button ("or pic a pic from your pics, erm.", height (40f))){
						change_mode ("pic_selector");
					};
				});
				
			}else{
				section (w => {
					GUILayout.Label ("Select a picture for your craft", header_label, width (w-100f));
					if(GUILayout.Button ("or enter a url", width (100f))){
						change_mode ("url_entry");
					};
				});
				
				if (pictures.Count > 0) {
					scroll_pos = scroll (scroll_pos, 620f, 300f, w => {
						foreach(List<PicData> row in groups){
							style_override = new GUIStyle ();
							style_override.normal.background = scroll_background;
							section (600f, sw => {
								foreach(PicData pic in row){
									v_section (150f, w2 => {
										if(GUILayout.Button (pic.texture, (hover_ele==pic.name ? pic_hover : pic_link), width (w2), height (w2*0.75f))){
											select_pic (pic);
										}
										if(GUILayout.Button (pic.name, (hover_ele==pic.name ? pic_hover : pic_link), width(w2))){
											select_pic (pic);
										}
									});
									if(GUILayoutUtility.GetLastRect ().Contains (Event.current.mousePosition)){
										hover_ele = pic.name;
									}
								}
							});
						}
					});
				}
				
				if(GUILayout.Button ("refresh")){
					prepare_pics ();
				}
				
			}
		}

		private void change_mode(string new_mode){
			mode = new_mode;
			autoheight ();
		}

		private void select_pic(PicData pic){
			KerbalX.editor_gui.pictures.Add (pic);
			this.hide ();
		}

		private List<FileInfo> picture_files(){
			DirectoryInfo dir = new DirectoryInfo (KerbalX.screenshot_dir);
			List<FileInfo> files = new List<FileInfo> ();
			
			//Get file info for all files of defined file_types within the given dir
			foreach(string file_type in file_types){
				foreach(FileInfo file in dir.GetFiles ("*." + file_type)){
					files.Add (file);
				}
			}
			return files;
		}

		private void prepare_pics(){
			List<FileInfo> files = picture_files ();
			pictures.Clear ();
			foreach(FileInfo file in files){
				//prepare the texture for the image
				Texture2D tex = new Texture2D (2, 2);
				byte[] pic_data = File.ReadAllBytes (file.FullName);
				tex.LoadImage (pic_data);

				//add a PicData struct for each picture into pictures (struct defines name, file and texture)
				PicData data = new PicData ();
				data.initialize (file.Name, file, tex);
				pictures.Add (data);
			}
			file_count = files.Count;
			group_pics (); //divide pictures into "rows" of x pics_per_row 
		}

		//constructs a List of Lists containing PicData.  Divides pictures into 'rows' of x pics_per_row 
		private void group_pics(){
			groups.Clear ();							//clear any existing groups
			groups.Add (new List<PicData>());			//add first row to groups
			int i = 0;
			foreach (PicData pic in pictures) {
				groups.Last ().Add (pic);				//add picture to the last row
				i++;
				if(i >= pics_per_row){					//once a row is full (row count == pics_per_row)
					groups.Add (new List<PicData>());	//then add another row to groups 
					i = 0;								//and reset i
				}
			}
		}
	}


	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class EditorActions : MonoBehaviour
	{
		private bool set_state = true;
		public string editor = null;

		private void Update(){
			if(set_state){
				set_state = false;
				KerbalX.console.window_pos = new Rect(250, 10, 310, 5);
				KerbalX.editor_gui.current_editor = EditorLogic.fetch.ship.shipFacility.ToString ();
			}
		}
	}

	[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
	public class KerbalXConsole : KerbalXWindow
	{
		private void Start()
		{
			window_title = "KX::Konsole";
			KerbalX.console = this;
			enable_request_handler ();
		}


		protected override void WindowContent(int win_id)
		{
			section (300f, e => { GUILayout.Label (KerbalX.last_log ());	});


			if(GUILayout.Button ("test 1")){
				HTTP http = HTTP.get ("http://localhost:3000/katateochi.json");
				http.set_header ("token", "foobar").send ((resp,code) => {
					Debug.Log (resp);
				});
			}

			if(GUILayout.Button ("ping production")){
				HTTP.get ("https://KerbalX.com/katateochi.json").send ((resp,code) => {
					Debug.Log (resp);
				});
			}


			if (GUILayout.Button ("open")) {
				//Foobar fb = gameObject.AddComponent (typeof(Foobar)) as Foobar;
				Foobar ff = gameObject.AddOrGetComponent<Foobar> ();
				Foobar.this_instance = ff;
			}
			if (GUILayout.Button ("close")) {
				Foobar ff = gameObject.AddOrGetComponent<Foobar> ();
				GameObject.Destroy (ff);
			}

			if (GUILayout.Button ("add text")) {
				Foobar ff = gameObject.AddOrGetComponent<Foobar> ();
				ff.some_text = "marmalade";
			}

			if (GUILayout.Button ("print log to console")) { KerbalX.show_log (); }
		}
	}

	//[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class Foobar : MonoBehaviour
	{
		private int window_id = 42;
		private Rect window_pos = new Rect(200, 200, 200, 200);

		public string some_text = "";
		public static Foobar this_instance = null;

		void Start(){
				
		}

		protected void OnGUI()
		{
			window_pos = GUILayout.Window (window_id, window_pos, DrawWindow, "testy moo");
		}

		public void DrawWindow(int win_id){

			GUILayout.Label ("this is a test");
			GUILayout.Label (some_text);
			GUI.DragWindow();

		}

	}


		
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class JumpStart : MonoBehaviour
	{
		public static bool autostart = false;
		public static string save_name = "default";
		public static string craft_name = "testy";

		public void Start()
		{
			if(autostart){
				HighLogic.SaveFolder = save_name;
				DebugToolbar.toolbarShown = true;
				var editor = EditorFacility.VAB;
				GamePersistence.LoadGame("persistent", HighLogic.SaveFolder, true, false);
				if(craft_name != null || craft_name != ""){					
					string path = Path.Combine (KSPUtil.ApplicationRootPath, "saves/" + save_name + "/Ships/VAB/" + craft_name + ".craft");
					EditorDriver.StartAndLoadVessel (path, editor);
				}else{
					EditorDriver.StartEditor (editor);
				}

			}
		}
	}


}
