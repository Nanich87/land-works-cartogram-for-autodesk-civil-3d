namespace Cartogram.Helpers
{
    using System;
    using System.Linq;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;

    internal static class TextHelper
    {
        public static ObjectId PromptForTextSelection()
        {
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptEntityOptions promptEntityOptions = new PromptEntityOptions("Изберете работна кота: ");
            promptEntityOptions.SetRejectMessage("Избраният обект не е MText!");
            promptEntityOptions.AddAllowedClass(typeof(MText), false);

            PromptEntityResult promptEntityResult = editor.GetEntity(promptEntityOptions);

            return promptEntityResult.ObjectId;
        }
    }
}