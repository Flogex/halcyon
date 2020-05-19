﻿using Halcyon.HAL;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using Microsoft.AspNetCore.Mvc;

namespace Halcyon.Web.HAL.Json {
    public class JsonHalOutputFormatter : IOutputFormatter, IApiResponseTypeMetadataProvider {
        public const string HalJsonType = "application/hal+json";

        private readonly IEnumerable<string> halJsonMediaTypes;
        private readonly NewtonsoftJsonOutputFormatter jsonFormatter;
        private readonly JsonSerializerSettings serializerSettings;

        public JsonHalOutputFormatter(
            MvcOptions mvcOptions,
            IEnumerable<string> halJsonMediaTypes = null)
        {
            if(halJsonMediaTypes == null) halJsonMediaTypes = new string[] { HalJsonType };

            this.serializerSettings = JsonSerializerSettingsProvider.CreateSerializerSettings();

            this.jsonFormatter = new NewtonsoftJsonOutputFormatter(this.serializerSettings, ArrayPool<Char>.Create(), mvcOptions);

            this.halJsonMediaTypes = halJsonMediaTypes;
        }

        public JsonHalOutputFormatter(
            MvcOptions mvcOptions,
            JsonSerializerSettings serializerSettings,
            IEnumerable<string> halJsonMediaTypes = null) {
            if(halJsonMediaTypes == null) halJsonMediaTypes = new string[] { HalJsonType };

            this.serializerSettings = serializerSettings;
            this.jsonFormatter = new NewtonsoftJsonOutputFormatter(this.serializerSettings, ArrayPool<Char>.Create(), mvcOptions);

            this.halJsonMediaTypes = halJsonMediaTypes;
        }

        public bool CanWriteResult(OutputFormatterCanWriteContext context) {
            return context.ObjectType == typeof(HALResponse) || jsonFormatter.CanWriteResult(context);
        }

        public async Task WriteAsync(OutputFormatterWriteContext context) {
            var halResponse = context.Object as HALResponse;
            if (halResponse == null)
            {
                await jsonFormatter.WriteAsync(context);
                return;
            }

            string mediaType = context.ContentType.HasValue ? context.ContentType.Value : null;

            object value = null;

            // If it is a HAL response but set to application/json - convert to a plain response
            var serializer = JsonSerializer.Create(this.serializerSettings);

            if(!halResponse.Config.ForceHAL && !halJsonMediaTypes.Contains(mediaType)) {
                value = halResponse.ToPlainResponse(serializer);
            } else {
                value = halResponse.ToJObject(serializer);
            }

            var jsonContext = new OutputFormatterWriteContext(context.HttpContext, context.WriterFactory, value.GetType(), value);
            jsonContext.ContentType = new StringSegment(mediaType);

            await jsonFormatter.WriteAsync(jsonContext);
        }

        public IReadOnlyList<string> GetSupportedContentTypes(string contentType, Type objectType)
        {
            var jsonTypes = jsonFormatter.GetSupportedContentTypes(contentType, objectType);

            // If we're not being asked about a specific type, send them all including the json types
            if (contentType == null)
            {
                // Add our hal types to the json types
                var allTypes = halJsonMediaTypes.ToList();
                allTypes.AddRange(jsonTypes);
                return allTypes;
            }

            // HAL types can't be subsets of those supported by the json formatter (correct?)
            // So return if supported json types are available
            if (jsonTypes != null)
                return jsonTypes;

            // Finally, return supported HAL types given that requested
            List<string> supportedHalTypes = null;
            var set = new MediaType(contentType);
            foreach (var halType in halJsonMediaTypes)
            {
                if (new MediaType(halType).IsSubsetOf(set))
                {
                    if (supportedHalTypes == null)
                        supportedHalTypes = new List<string>();
                    supportedHalTypes.Add(halType);
                }
            }

            return supportedHalTypes;
        }
    }
}
