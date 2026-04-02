using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartArchive.Filters
{
    /// <summary>
    /// Schema filter to handle IFormFile in multipart/form-data requests
    /// </summary>
    public class FormFileSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(IFormFile))
            {
                schema.Type = "string";
                schema.Format = "binary";
                schema.Description = "File to upload";
            }
        }
    }

    /// <summary>
    /// Operation filter to properly configure multipart/form-data for methods with IFormFile parameters
    /// </summary>
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var parameters = context.MethodInfo.GetParameters();
            var hasFormFile = parameters.Any(p =>
            {
                var paramType = p.ParameterType;
                // Check if parameter type is AnalyzeRequest which contains IFormFile
                if (paramType.Name == "AnalyzeRequest")
                {
                    var fileProperty = paramType.GetProperty("File");
                    return fileProperty?.PropertyType == typeof(IFormFile);
                }
                return paramType == typeof(IFormFile);
            });

            if (!hasFormFile)
                return;

            // Clear existing parameters and rebuild for form-data
            operation.Parameters.Clear();
            
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    {
                        "multipart/form-data",
                        new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>
                                {
                                    {
                                        "file",
                                        new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "File to upload"
                                        }
                                    }
                                },
                                Required = new HashSet<string> { "file" }
                            }
                        }
                    }
                },
                Required = true
            };
        }
    }
}

