﻿using System;
using UnityEngine;
using System.Collections.Generic;

namespace KerbalX
{

	public struct DropdownData{
		public int id;
		public bool show_select;
		public Vector2 scroll_pos;
		public void selected(int set_id){
			id = set_id;
		}
	}

	/* KerbalXWindow is a base class to be inherited by classes which draw GUI windows
	It provides common setup required to draw a GUI window, enabling DRY and minimal window classes.
	A class which inherits KerbalXWindow needs to override the WindowContent method to define the content of the window
	Basic Usage:
		[KSPAddon(KSPAddon.Startup.MainMenu, false)]
		public class SimpleWindow : KerbalXWindow
		{
			protected override void WindowContent(int win_id)
			{
				GUILayout.Label ("some nonsense", GUILayout.Width (60f));
			}
		}
	Attributes like the window title, size/pos, id are set, but can be overridden by defining a Start() method
	Configured Usage:
		[KSPAddon(KSPAddon.Startup.MainMenu, false)]
		public class SimpleWindow : KerbalXWindow
		{
			private void Start()
			{
				window_pos = new Rect(100,100,500,200); //defaults to new Rect() if not set
				window_title = "test window";			//defaults to "untitled window" if not set
				window_id = 42;							//defaults to the next ID in sequence (change last_window_id in the base class to change the sequence start point)
				footer = false							//defaults to true. if true adds a common set of GUI elements defined in DrawWindow
				draggable = false;						//defaults to true. makes the windows draggable, duh. 
			}

			protected override void WindowContent(int win_id)
			{
				GUILayout.Label ("some nonsense", GUILayout.Width (60f));
			}
		}	 
	KerbalXWindow also provides the handy-dandy fabtastic grid method. grid takes a width and a lambda (delegate) statement and wraps the actions defined in the
	lambda in calls to BeginHorizontal and EndHorizontal.  This ensures End is always called after a begin, and (I think) makes for clearer and more readable code.
 	*/
	public class KerbalXWindow : MonoBehaviour
	{
		public Rect window_pos = new Rect();
		protected string window_title = "untitled window";
		protected int window_id = 0;
		protected bool footer = true;
		protected bool draggable = true;
		static int last_window_id = 0;



		//Definition of delegate to be passed into the section method 
		protected delegate void Content(float width);

		/* Essentially wraps the function of a delegate in calls to BeginHorizontal and EndHorizontal
		Takes a width float or string and a delegate/statement lambda and wraps the actions defined by the lambda in Being/EndHorizontals
		The value for width is passed to the BeginHorizontal and can either be given as a float or a string 
		If a string is used for the width it treats the given value to be x% of the window width
		The lambda is passed a Dictionary containing keys 'width' (the width float) and 'pos' (the Rect for the window)
		Usage:
			grid (width, win => {
				// Calls to draw GUI elements inside a BeginHorizontal group ie;
				// GUILayout.Label ("some nonsense", GUILayout.Width (60f));
				// you can use win["width"] and win["pos"] inside the block
			});	
		*/
		protected void section(float width, Content content)
		{
			GUILayout.BeginHorizontal(GUILayout.Width(width), GUILayout.MaxWidth (width));
			content((float)width);
			GUILayout.EndHorizontal ();
		}

		protected void v_section(float width, Content content){
			GUILayout.BeginVertical (GUILayout.Width(width), GUILayout.MaxWidth (width));
			content (width);
			GUILayout.EndVertical ();
		}

		protected Vector2 scroll(Vector2 scroll_pos, float width, float height, Content content){
			scroll_pos = GUILayout.BeginScrollView(scroll_pos, GUILayout.Width(width), GUILayout.Height(height));
			content (width);
			GUILayout.EndScrollView();
			return scroll_pos;
		}

		protected DropdownData dropdown(Dictionary<int, string> collection, DropdownData drop_data, float outer_width, float menu_height){
			GUIStyle dropdown_field = new GUIStyle (GUI.skin.textField);
			GUIStyle dropdown_menu_item = new GUIStyle (GUI.skin.label);
			//dropdown_menu_item.normal.textColor = Color.magenta;
			dropdown_menu_item.onHover.textColor = new Color (0.4f,0.5f,0.9f,1); //color also known as KerbalX Blue - #6E91EB
			dropdown_menu_item.hover.textColor = new Color (0.4f,0.5f,0.9f,1); //color also known as KerbalX Blue - #6E91EB
			dropdown_menu_item.padding = new RectOffset (0, 0, 0, 0);

			string selected;
			collection.TryGetValue (drop_data.id, out selected);

			v_section (outer_width, (width) => {
				section (width, w => {
					if (GUILayout.Button (selected, dropdown_field, GUILayout.Width (width - 20) )) {
						drop_data.show_select = !drop_data.show_select;	
					}
					if (GUILayout.Button ("\\/", GUILayout.Width (20f) )) {
						drop_data.show_select = !drop_data.show_select;	
					}
				});
				section (width, w => {
					if(drop_data.show_select){
						drop_data.scroll_pos = scroll (drop_data.scroll_pos, w, menu_height, (w2) => {
							foreach(KeyValuePair<int, string> item in collection){
								if(GUILayout.Button (item.Value, dropdown_menu_item, GUILayout.Width (w2-25))){
									drop_data.selected (item.Key);
									drop_data.show_select = false;
								}
							}
						});
					}
				});
			});
			return drop_data;
		}

		protected float pcent(string percent, object width_in){
			float p = float.Parse (percent.Replace ("%", ""));
			float w = (float)width_in;
			return (float)Math.Floor ((w / 100) * p);
		}

		//called on each frame, handles drawing the window and will assign the next window id if it's not set
		protected void OnGUI()
		{
			if(window_id == 0){
				window_id = last_window_id + 1;
				last_window_id = last_window_id + 1;
			}
			window_pos = GUILayout.Window (window_id, window_pos, DrawWindow, window_title,  GUILayout.Width( window_pos.width ), GUILayout.ExpandHeight (true));
		}

		//Callback methods which is passed to GUILayout.Window in OnGUI.  Calls WindowContent and performs common window actions
		private void DrawWindow(int window_id)
		{
			GUIStyle link_label_style = new GUIStyle (GUI.skin.label);
			link_label_style.normal.textColor = new Color (0.4f,0.5f,0.9f,1); //color also known as KerbalX Blue - #6E91EB

			//GUI.BringWindowToFront(window_id);
			WindowContent (window_id);			//Draw the main content of the window as defined by WindowContent
			if(footer){
				if(GUILayout.Button ("KerbalX.com", link_label_style)){
					Application.OpenURL (KerbalX.site_url);
				}
				GUILayout.Label ("window id: " + window_id);
			}
			if(draggable){
				GUI.DragWindow();
			}
		}

		//The main method which defines the content of a window.  This method is provided so as to be overridden in inherited classes
		protected virtual void WindowContent(int window_id)
		{

		}

		private void onDestroy(){
			print ("shit was destroyed yo"); 

		}

	}
}

