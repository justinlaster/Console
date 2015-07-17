﻿using System;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Cognifide.PowerShell.Core.Settings;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Resources;
using Sitecore.Shell.Applications.ContentManager.Galleries;
using Sitecore.Web;
using Sitecore.Web.UI;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Web.UI.XmlControls;
using Control = Sitecore.Web.UI.HtmlControls.Control;
using ListItem = Sitecore.Web.UI.HtmlControls.ListItem;

namespace Cognifide.PowerShell.Client.Controls
{
    public class MruGallery : GalleryForm
    {
        // Fields
        protected DataContext ContentDataContext;
        protected TreeviewEx ContentTreeview;
        protected Combobox Databases;
        protected string InitialDatabase;
        protected Scrollbox Scripts;
        protected Edit SearchPhrase;
        protected GalleryMenu SearchResults;
        public Item ItemFromQueryString { get; set; }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            if (message.Name != "event:click" && message.Name != "datacontext:changed" && message.Name != "event:change" &&
                message.Name != "event:keypress")
            {
                Invoke(message, true);
            }
        }

        // Methods
        protected void ContentTreeview_Click()
        {
            var folder = ContentDataContext.GetFolder();
            if (folder != null && folder.TemplateName == TemplateNames.ScriptTemplateName)
            {
                Load(folder.Uri.ToString());
            }
        }

        protected void Load(string uri)
        {
            Assert.ArgumentNotNull(uri, "uri");
            var uri2 = ItemUri.Parse(uri);
            if (uri2 != null)
            {
                var item = Database.GetItem(uri2);
                if (item != null)
                {
                    SheerResponse.Eval(
                        string.Concat("scForm.getParentForm().invoke(\"ise:mruopen(id=", item.ID, ",language=",
                            item.Language, ",version=", item.Version, ",db=", item.Database.Name, ")\")"));
                }
            }
        }

        protected void ExecuteMruItem(string command)
        {
            Assert.ArgumentNotNull(command, "command");
            if (command != null)
            {
                SheerResponse.Eval(
                    string.Concat("scForm.getParentForm().invoke(\"", command, "\")"));
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            ItemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);
            if (!Context.ClientPage.IsEvent)
            {
                Item item;
                Item item2;
                Item[] itemArray;
                ChangeSearchPhrase();

                var db = WebUtil.GetQueryString("contextDb");
                var itemId = WebUtil.GetQueryString("contextItem");
                InitialDatabase =
                    string.IsNullOrEmpty(db) || "core".Equals(db, StringComparison.OrdinalIgnoreCase)
                        ? ApplicationSettings.ScriptLibraryDb
                        : db;

                BuildDatabases();


                ContentDataContext.GetFromQueryString();
                ContentDataContext.BeginUpdate();
                ContentDataContext.Parameters = "databasename=" + InitialDatabase;
                ContentTreeview.RefreshRoot();
                ContentDataContext.Root = ApplicationSettings.ScriptLibraryRoot().ID.ToString();

                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(db))
                {
                    ContentDataContext.SetFolder(Factory.GetDatabase(db).GetItem(itemId).Uri);
                }

                ContentDataContext.EndUpdate();
                ContentTreeview.RefreshRoot();

                ContentDataContext.GetFromQueryString();
                ContentDataContext.GetState(out item, out item2, out itemArray);
                if (itemArray.Length > 0)
                {
                    ContentDataContext.Folder = itemArray[0].ID.ToString();
                }
            }
        }

        private void BuildDatabases()
        {
            foreach (var str in Factory.GetDatabaseNames())
            {
                if (!string.Equals(str, "core", StringComparison.OrdinalIgnoreCase) &&
                    !Sitecore.Client.GetDatabaseNotNull(str).ReadOnly)
                {
                    var child = new ListItem();
                    Databases.Controls.Add(child);
                    child.ID = Control.GetUniqueID("ListItem");
                    child.Header = str;
                    child.Value = str;
                    child.Selected = str.Equals(InitialDatabase, StringComparison.InvariantCultureIgnoreCase);
                }
            }
        }

        protected void ChangeDatabase()
        {
            var name = Databases.SelectedItem.Value;
            ContentDataContext.BeginUpdate();
            ContentDataContext.Parameters = "databasename=" + name;
            ContentDataContext.EndUpdate();
            ContentTreeview.RefreshRoot();
        }

        protected void ChangeSearchPhrase()
        {
            var parts = (SearchPhrase.Value ?? string.Empty).Split(':');
            var database = parts.Length > 1 ? parts[0] : string.Empty;
            var phrase = parts.Length > 1 ? parts[1] : parts[0];
            var recentHeader = new MenuHeader();
            SearchResults.Controls.AddAt(0, recentHeader);
            Scripts.Controls.Clear();
            Scripts.Class = Scripts.Class + " scDontStretch";
            SearchResults.Class = Scripts.Class + " scDontStretch";
            if (string.IsNullOrEmpty(phrase))
            {
                recentHeader.Header = "Most Recently opened scripts:";
                foreach (Item item in ApplicationSettings.GetIseMruContainerItem().Children)
                {
                    var messageString = item["Message"];
                    var message = Message.Parse(null, messageString);
                    var db = message.Arguments["db"];
                    var id = message.Arguments["id"];
                    var scriptItem = Factory.GetDatabase(db).GetItem(id);
                    if (scriptItem != null)
                    {
                        RenderRecent(scriptItem);
                    }
                }

            }
            else if (database.Length > 0)
            {
                recentHeader.Header = "Scripts matching: '" + phrase + "' in '" + database + "*' database";
                foreach (var index in ContentSearchManager.Indexes)
                {
                    if (index.Name.StartsWith("sitecore_" + database) &&
                        index.Name.EndsWith("_index"))
                    {
                        SearchDatabase(index.Name, phrase);
                    }
                }
            }
            else
            {
                recentHeader.Header = "Scripts matching: '" + phrase + "' in all databases";
                var masterIndex = "sitecore_" + ApplicationSettings.ScriptLibraryDb + "_index";
                SearchDatabase(masterIndex, phrase);
                foreach (var index in ContentSearchManager.Indexes)
                {
                    if (!string.Equals(masterIndex, index.Name, StringComparison.OrdinalIgnoreCase) &&
                        index.Name.StartsWith("sitecore_") &&
                        index.Name.EndsWith("_index"))
                    {
                        SearchDatabase(index.Name, phrase);
                    }
                }
            }
            HtmlTextWriter writer = new HtmlTextWriter(new StringWriter());
            SearchResults.RenderControl(writer);
            SheerResponse.SetOuterHtml(SearchResults.ID, writer.InnerWriter.ToString());
        }

        private void SearchDatabase(string indexName, string phrase)
        {
            using (
                var context =
                    ContentSearchManager.GetIndex(indexName)
                        .CreateSearchContext())
            {
                // get all items in medialibrary
                var rootID = ApplicationSettings.ScriptLibraryRoot().ID.ToShortID().ToString();
                var query =
                    context.GetQueryable<SearchResultItem>()
                        .Where(
                            i =>
                                i["_path"].Contains(rootID) &&
                                i["_templatename"] == "PowerShell Script").Take(10);
                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    query = query.Where(i => i["_name"].Contains(phrase));
                }
                foreach (var result in query)
                {
                    var scriptItem = result.GetItem();
                    RenderRecent(scriptItem);
                }
            }
        }

        private void RenderRecent(Item scriptItem)
        {
            var control = ControlFactory.GetControl("MruGallery.SearchItem") as XmlControl;
            Assert.IsNotNull(control, typeof (XmlControl), "Xml Control \"{0}\" not found",
                "MruGallery.SearchItem");

            Context.ClientPage.AddControl(Scripts, control);

            var iconUrl = scriptItem.Appearance.Icon;
            if (!string.IsNullOrWhiteSpace(iconUrl))
            {
                var builder = new ImageBuilder
                {
                    Src = Images.GetThemedImageSource(iconUrl, ImageDimension.id16x16),
                    Class = "scRibbonToolbarSmallGalleryButtonIcon",
                    Alt = scriptItem.DisplayName
                };
                iconUrl = builder.ToString();
            }

            var currentScript = ItemFromQueryString != null && ItemFromQueryString.ID == scriptItem.ID &&
                                ItemFromQueryString.Database.Name == scriptItem.Database.Name;

            control["ScriptIcon"] = "<div class=\"versionNum\">" + iconUrl + "</div>";
            control["Location"] = Translate.Text("{0}",
                scriptItem.Paths.ParentPath.Substring(ApplicationSettings.ScriptLibraryPath.Length));
            control["Database"] = Translate.Text("{0}", scriptItem.Database.Name);
            control["Name"] = Translate.Text("{0}", scriptItem.DisplayName);
            control["Click"] = string.Format("ExecuteMruItem(\"ise:mruopen(id={0},db={1})\")", scriptItem.ID,
                scriptItem.Database.Name);
            control["Class"] = currentScript ? "selected" : string.Empty;
        }
    }
}