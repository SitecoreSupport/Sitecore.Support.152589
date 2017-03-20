namespace Sitecore.Support.Data.Fields
{
  using System;
  using Sitecore.Data.Fields;
  using Sitecore.Links;
  using Sitecore.Diagnostics;
  using Sitecore.Resources.Media;
  using Sitecore.Data;
  using Sitecore.SecurityModel;

  public class HtmlField : Sitecore.Data.Fields.HtmlField
  {
    public HtmlField(Field innerField) : base(innerField)
    {
    }

    public override void ValidateLinks(LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");

      base.ValidateLinks(result);

      // The main idea of the patch is to review broken links (those with TargetItemID == ID.Null) 
      // and revise valid media src links e.g. <img src="-/media/mediaitem.ashx" /> using MediaRequest class
      // to avoid copy-pasting code (we expose the class that really used for handling those media requests).

      // MediaRequestHelper derives from MediaRequest to bypass some limitations, check xml comments behind.
      var mediaRequestHelper = new MediaRequestHelper(InnerField.Database);

      // security disabler is necessary to make sure validation works correctly even for secured media items
      using (new SecurityDisabler())
      {
        // get a copy of the links list to be able to modify original in foreach loop
        var links = result.Links.ToArray();

        foreach (var link in links)
        {
          if (link.TargetItemID == ID.Null)
          {
            var targetPath = link.TargetPath;
            try
            {
              var itemPath = mediaRequestHelper.GetMediaPath(targetPath);
              var targetItem = InnerField.Database.GetItem(itemPath);
              if (targetItem != null)
              {
                result.AddValidLink(targetItem, targetPath);
                result.Links.Remove(link);
              }
            }
            catch (Exception ex)
            {
              LogError(link, ex);
            }
          }
        }
      }
    }

    private void LogError(ItemLink link, Exception ex)
    {
      var sourceItemPath = "";
      try
      {
        sourceItemPath = link.GetSourceItem()?.Paths.FullPath;
      }
      catch
      {
        // it is not important if we cannot get source item path
      }

      // sample value: master://{110D559F-DEA5-42EA-9C1C-8A5DF7E70EF9}/sitecore/content/Home?field={A60ACD61-A6DB-4182-8329-C957982CEC74}&lang=en&ver=1
      var source = $"{link.SourceDatabaseName}://{link.SourceItemID}{sourceItemPath}?field={link.SourceFieldID}&lang={link.SourceItemLanguage}&ver={link.SourceItemVersion?.Number}";

      Log.Error($"Failed to revise potentially-valid broken link, TargetPath: {link.TargetPath}, Source: {source}", ex, this);
    }

    /// <summary>
    ///   This class exists to make GetMediaPath method public and to specify database to avoid NullReference exception 
    ///   (in base class it is resolved with help of HttpRequest which is not available here in certain conditions).
    /// </summary>
    private class MediaRequestHelper : MediaRequest
    {
      private Database Database { get; }

      public MediaRequestHelper(Database database)
      {
        Database = database;
      }

      protected override Database GetDatabase()
      {
        return Database;
      }

      public new string GetMediaPath(string localPath)
      {
        return base.GetMediaPath(localPath);
      }
    }
  }
}
