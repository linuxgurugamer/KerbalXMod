﻿

//██╗  ██╗███████╗██████╗ ██████╗  █████╗ ██╗     ██╗  ██╗    ███╗   ███╗ ██████╗ ██████╗ 
//██║ ██╔╝██╔════╝██╔══██╗██╔══██╗██╔══██╗██║     ╚██╗██╔╝    ████╗ ████║██╔═══██╗██╔══██╗
//█████╔╝ █████╗  ██████╔╝██████╔╝███████║██║      ╚███╔╝     ██╔████╔██║██║   ██║██║  ██║
//██╔═██╗ ██╔══╝  ██╔══██╗██╔══██╗██╔══██║██║      ██╔██╗     ██║╚██╔╝██║██║   ██║██║  ██║
//██║  ██╗███████╗██║  ██║██████╔╝██║  ██║███████╗██╔╝ ██╗    ██║ ╚═╝ ██║╚██████╔╝██████╔╝
//╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═════╝ ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝    ╚═╝     ╚═╝ ╚═════╝ ╚═════╝ 
// ____              _  __          _             _                           _       _ 
//|  _ \            | |/ /         | |           | |                         | |     (_)
//| |_) |  _   _    | ' /    __ _  | |_    __ _  | |_    ___    ___     ___  | |__    _ 
//|  _ <  | | | |   |  <    / _` | | __|  / _` | | __|  / _ \  / _ \   / __| | '_ \  | |
//| |_) | | |_| |   | . \  | (_| | | |_  | (_| | | |_  |  __/ | (_) | | (__  | | | | | |
//|____/   \__, |   |_|\_\  \__,_|  \__|  \__,_|  \__|  \___|  \___/   \___| |_| |_| |_|
//          __/ |                                                                       
//         |___/                                                                        


//Built Against KSP 1.4.3
//build id = 02152
//2018-04-26_22-43-47



using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using KXAPI;

namespace KerbalX
{
    //The KerbalX class acts as a holder for various static variables that need to persist across scenes.
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class KerbalX : MonoBehaviour
    {

        //Static Variables
        internal static string version = "1.1.0";
        internal static KerbalXAPI api = new KerbalXAPI("KerbalXMod", KerbalX.version);

        internal static string screenshot_dir;
        internal static KerbalXSettings settings = new KerbalXSettings();

        internal static bool failed_to_connect          = false;
        internal static string server_error_message     = null;
        internal static bool upgrade_required           = false;
        internal static string upgrade_required_message = null;

        internal static List<string> log_data = new List<string>();
//        internal static Dictionary<int, Dictionary<string, string>> existing_craft;//container for listing of user's craft already on KX and some details about them.
        internal static Dictionary<int, Dictionary<string, string>> existing_craft {
            get {
                return KerbalX.api.user_craft;
            }
        }

        //window handles (cos a window without a handle is just a pane)
        internal static KerbalXWindow console                         = null;
//        internal static KerbalXLoginInterface login_gui               = null;
        internal static KerbalXUploadInterface upload_gui             = null;
        internal static KerbalXDownloadInterface download_gui         = null;
        internal static KerbalXImageSelector image_selector           = null;
        internal static KerbalXActionGroupInterface action_group_gui  = null;

        //Toolbar Buttons
        internal static ApplicationLauncherButton upload_gui_toolbar_button   = null;
        internal static ApplicationLauncherButton download_gui_toolbar_button = null;
        internal static ApplicationLauncherButton console_button              = null;


        //logging, not suitable for lumberjacks
        internal static void log(string s) { 
            s = "[KerbalX] " + s;
            log_data.Add(s); 
            Debug.Log(s);
        }




        //Instance Methods - Toolbar Initialization

        //addd listeners for when the application launcher is ready to take instructions
        private void Awake(){
            GameEvents.onGUIApplicationLauncherReady.Add(add_to_toolbar);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(remove_from_toolbar);
            GameEvents.onGameSceneLoadRequested.Add(scene_load_request);
            KerbalXDownloadController.query_new_save = true;
        }


        private void Start(){
            KerbalX.api.login();
        }

        //Trigger the creation of custom Skin (copy of default GUI.skin with various custom styles added to it)
        private void OnGUI(){
            if(KerbalXWindow.KXskin == null){
                StyleSheet.prepare();
            }
        }

        //Bind events to add buttons to the toolbar
        private void add_to_toolbar(){
            ApplicationLauncher.Instance.AddOnHideCallback(this.toolbar_on_hide);     //bind events to close guis when toolbar hides

            KerbalX.log("Adding buttons to toolbar");

            if(!KerbalX.upload_gui_toolbar_button){
                KerbalX.upload_gui_toolbar_button = ApplicationLauncher.Instance.AddModApplication(
                    toggle_upload_interface, toggle_upload_interface, 
                    upload_btn_hover_on, upload_btn_hover_off, 
                    null, null, 
                    ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH, 
                    StyleSheet.assets["upload_toolbar_btn"]
                );
            }
            if(!KerbalX.download_gui_toolbar_button){
                KerbalX.download_gui_toolbar_button = ApplicationLauncher.Instance.AddModApplication(
                    toggle_download_interface, toggle_download_interface, 
                    download_btn_hover_on, download_btn_hover_off,
                    null, null, 
                    ApplicationLauncher.AppScenes.SPACECENTER,
                    StyleSheet.assets["dnload_toolbar_btn"]
                );
            }
//            if(!KerbalX.console_button){
//                KerbalX.console_button = ApplicationLauncher.Instance.AddModApplication(
//                    toggle_console, toggle_console, 
//                    null, null,
//                    null, null, 
//                    ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH, 
//                    GameDatabase.Instance.GetTexture(Paths.joined("KerbalX", "Assets", "console_button"), false)
//                );
//            }
        }

        //remove any existing KX buttons from the toolbar
        private void remove_from_toolbar(){
            KerbalX.log("Removing buttons from toolbar");
            if(KerbalX.upload_gui_toolbar_button){
                ApplicationLauncher.Instance.RemoveModApplication(KerbalX.upload_gui_toolbar_button);
                KerbalX.upload_gui_toolbar_button = null;
            }
            if(KerbalX.download_gui_toolbar_button){
                ApplicationLauncher.Instance.RemoveModApplication(KerbalX.download_gui_toolbar_button);
                KerbalX.download_gui_toolbar_button = null;
            }
            if(KerbalX.console_button){
                ApplicationLauncher.Instance.RemoveModApplication(KerbalX.console_button);
                KerbalX.console_button = null;
            }
        }

        //triggered by scene load, calls removal of the buttons
        private void scene_load_request(GameScenes scene){
            remove_from_toolbar();
        }

        //triggered when the application launcher hides, used to teardown any open GUIs
        private void toolbar_on_hide(){
            if(KerbalX.upload_gui){
                GameObject.Destroy(KerbalX.upload_gui);
            }
            if(KerbalX.download_gui){
                GameObject.Destroy(KerbalX.download_gui);
            }
        }


        //Button Actions

        //Action for upload interface button
        private void toggle_upload_interface(){
            if(KerbalX.upload_gui){
                KerbalX.upload_gui.toggle();
            } else{
                KerbalX.log("UploadInterface has not been started");
            }
        }

        //Action for download interface button
        private void toggle_download_interface(){
            if(KerbalX.download_gui){
                if(KerbalX.download_gui.visible){
                    KerbalX.download_gui.hide();
                }else{
                    KerbalXDownloadController.instance.fetch_download_queue(true);
                }
            } else{
                KerbalX.log("DownloadInterface has not been started");
            }
        }

        //Action for console button.
        private void toggle_console(){
            if(KerbalX.console){
                KerbalX.console.toggle();
            }
        }


        //Button hover actions

        private void upload_btn_hover_on(){
            KerbalX.upload_gui_toolbar_button.SetTexture(StyleSheet.assets["upload_toolbar_btn_hover"]);
        }
        private void upload_btn_hover_off(){
            KerbalX.upload_gui_toolbar_button.SetTexture(StyleSheet.assets["upload_toolbar_btn"]);
        }
        private void download_btn_hover_on(){
            KerbalX.download_gui_toolbar_button.SetTexture(StyleSheet.assets["dnload_toolbar_btn_hover"]);
        }
        private void download_btn_hover_off(){
            KerbalX.download_gui_toolbar_button.SetTexture(StyleSheet.assets["dnload_toolbar_btn"]);
        }

    }
}
