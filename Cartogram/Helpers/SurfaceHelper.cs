namespace Cartogram.Helpers
{
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.Civil.DatabaseServices;

    internal static class SurfaceHelper
    {
        public static TinSurface PromptForSurfaceSelection(string message)
        {
            Database database = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Database;
            Editor editor = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument.Editor;

            PromptEntityOptions promptEntityOptions = new PromptEntityOptions(string.Format("{0}: ", message));
            promptEntityOptions.SetRejectMessage("Избраният обект не е повърхнина!");
            promptEntityOptions.AddAllowedClass(typeof(TinSurface), true);

            PromptEntityResult promptEntityResult = editor.GetEntity(promptEntityOptions);
            if (promptEntityResult.Status != PromptStatus.OK)
            {
                return null;
            }

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                TinSurface tinSurface = transaction.GetObject(promptEntityResult.ObjectId, OpenMode.ForRead) as TinSurface;

                return tinSurface;
            }
        }
    }
}