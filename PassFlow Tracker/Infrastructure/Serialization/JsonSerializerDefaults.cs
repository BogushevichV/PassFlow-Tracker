using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PassFlow_Tracker.Infrastructure.Serialization
{
    public static class JsonSerializerDefaults
    {
        public static readonly JsonSerializerOptions SafeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = 32,                                    
            NumberHandling = JsonNumberHandling.Strict,       
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static readonly JsonSerializerOptions OutputOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,                            
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static readonly JsonSerializerOptions FileImportOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = 64,                                    
            AllowTrailingCommas = true,                       
            ReadCommentHandling = JsonCommentHandling.Skip    
        };
    }
}
