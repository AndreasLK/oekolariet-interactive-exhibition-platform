using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.Games.Requests
{
        /// <summary>
        /// The body sent by the editor when a game gets a new template.
        /// </summary>
        /// <param name="TemplateId">The template to use.</param>
        public sealed record UpdateGameTemplateRequest(Guid TemplateId);
}
