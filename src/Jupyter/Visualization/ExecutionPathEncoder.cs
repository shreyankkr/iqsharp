using System;
using Microsoft.Jupyter.Core;

namespace Microsoft.Quantum.IQSharp.Jupyter
{
    public class ExecutionPathDisplayable
    {
        public string Id;
    }

    public class ExecutionPathToHtmlEncoder : IResultEncoder
    {
        /// <inheritdoc />
        public string MimeType => MimeTypes.Html;

        /// <summary>
        ///     Checks if a given display object is an ExecutionPath object, and if so,
        ///     returns its visualization into an HTML doc.
        /// </summary>
        public EncodedData? Encode(object displayable)
        {
            if (displayable is ExecutionPathDisplayable dis)
            {
                var script = $"<div id='{dis.Id}' />";
                return script.ToEncodedData();
            }
            else return null;
        }
    }
}