using System;
using GamepadMapperGUI.Models;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Services.Infrastructure;

public static class CommunityTemplateSubmissionClone
{
    public static GameProfileTemplate CloneForSubmission(
        GameProfileTemplate source,
        string catalogFolder,
        string authorForJson,
        string listingDescription)
    {
        var json = JsonConvert.SerializeObject(source);
        var clone = JsonConvert.DeserializeObject<GameProfileTemplate>(json)
                    ?? throw new InvalidOperationException("Template clone failed.");
        clone.TemplateCatalogFolder = catalogFolder;
        clone.Author = authorForJson;
        clone.CommunityListingDescription = listingDescription;
        return clone;
    }
}
