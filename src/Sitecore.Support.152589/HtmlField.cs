using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.Resources.Media;
using Sitecore.SecurityModel;
using System;

namespace Sitecore.Support.Data.Fields
{
  public class HtmlField : Sitecore.Data.Fields.HtmlField
  {
    private class MediaRequestHelper : MediaRequest
    {
      private Database Database
      {
        get;
      }

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

    public HtmlField(Field innerField)
        : base(innerField)
    {
    }

    public override void ValidateLinks(LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");
      base.ValidateLinks(result);
      MediaRequestHelper mediaRequestHelper = new MediaRequestHelper(base.InnerField.Database);
      using (new SecurityDisabler())
      {
        ItemLink[] array = result.Links.ToArray();
        foreach (ItemLink itemLink in array)
        {
          if (itemLink.TargetItemID == ID.Null)
          {
            string targetPath = itemLink.TargetPath;
            try
            {
              string mediaPath = mediaRequestHelper.GetMediaPath(targetPath);
              Item item = base.InnerField.Database.GetItem(mediaPath);
              if (item != null)
              {
                result.AddValidLink(item, targetPath);
                result.Links.Remove(itemLink);
              }
            }
            catch (Exception ex)
            {
              LogError(itemLink, ex);
            }
          }
        }
      }
    }

    private void LogError(ItemLink link, Exception ex)
    {
      string text = "";
      try
      {
        text = link.GetSourceItem()?.Paths.FullPath;
      }
      catch
      {
      }
      string arg = $"{link.SourceDatabaseName}://{link.SourceItemID}{text}?field={link.SourceFieldID}&lang={link.SourceItemLanguage}&ver={link.SourceItemVersion?.Number}";
      Log.Error($"Failed to revise potentially-valid broken link, TargetPath: {link.TargetPath}, Source: {arg}", ex, this);
    }
  }
}