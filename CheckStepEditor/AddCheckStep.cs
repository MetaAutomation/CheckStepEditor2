////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  MetaAutomation (C) 2018 by Matt Griscom.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CheckStepEditor
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AddCheckStep
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("d870339a-2fc4-41fb-b39c-28d51979f3f6");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddCheckStep"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private AddCheckStep(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);

            var command = new OleMenuCommand(this.Execute, menuCommandID);

            command.BeforeQueryStatus += QueryButtonStatus;
            commandService.AddCommand(/*menuItem*/command);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static AddCheckStep Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Verify the current thread is the UI thread - the call to AddCommand in AddCheckStep's constructor requires
            // the UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new AddCheckStep(package, commandService);
        }

        private EnvDTE80.DTE2 m_dte = null;
        EnvDTE80.DTE2 GetDTE()
        {
            if (m_dte == null)
            {
                m_dte = ((IServiceProvider)package).GetService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            }
            return m_dte;
        }

        private void QueryButtonStatus(object sender, EventArgs e)
        {
            var button = (MenuCommand)sender;

            // Make the button invisible by default
            button.Visible = false;

            EnvDTE.ProjectItem item = this.GetDTE().SelectedItems.Item(1)?.ProjectItem;

            if (item != null)
            {
                string fileExtension = Path.GetExtension(item.Name).ToLowerInvariant();
                string[] supportedFiles = new[] { ".cs" };

                // Show the button only if a supported file is selected
                button.Enabled = button.Visible = supportedFiles.Contains(fileExtension);
            }
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.TextDocument activeTextDocument = (EnvDTE.TextDocument)this.GetDTE().ActiveDocument.Object("TextDocument");
            EnvDTE.TextSelection selection = activeTextDocument.Selection;

            this.SubstituteStringFromSelection_AddCheckStep(activeTextDocument);
        }

        /// <summary>
        /// Put the selected lines of code inside a check step.
        /// This method does all modification of the text in the editor, with one string substitution
        /// </summary>
        /// <param name="document"></param>
        /// <param name="selection"></param>
        private void SubstituteStringFromSelection_AddCheckStep(EnvDTE.TextDocument document)
        {
            EnvDTE.EditPoint earlierPoint = null, laterPoint = null;
            EditingTools.Instance.GetEditPointsForLinesToCheck(document, out earlierPoint, out laterPoint);
            string indentSpaces = EditingTools.Instance.GetIndentSpaces(document);
            string linesNoTabs = earlierPoint.GetText(laterPoint).Replace("\t", indentSpaces);
            string[] lines = linesNoTabs.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            StringBuilder resultingText = new StringBuilder();
            bool firstLine = true;
            string originalIndent = string.Empty;

            foreach (string line in lines)
            {
                if (!firstLine)
                {
                    resultingText.Append(Environment.NewLine);
                }

                firstLine = false;

                if (!String.IsNullOrWhiteSpace(line))
                {
                    string totalIndentSpaces = "";
                    int tabbedSpacesCounter = 0;

                    // Found how many tabbed spaces precede the line text
                    while ((line.Length >= ((tabbedSpacesCounter + 1) * document.TabSize)) && (line.Substring(tabbedSpacesCounter * document.TabSize, document.TabSize) == indentSpaces))
                    {
                        totalIndentSpaces += indentSpaces;
                        tabbedSpacesCounter++;
                    }

                    // find the minimum tab count, but skipping any zero-tab line
                    if (originalIndent.Length == 0)
                    {
                        originalIndent = totalIndentSpaces;
                    }
                    else
                    {
                        if (totalIndentSpaces.Length < originalIndent.Length)
                        {
                            originalIndent = totalIndentSpaces;
                        }
                    }

                    // Start the resulting line with a tab
                    resultingText.Append(indentSpaces);
                }

                // Add the rest of the text
                resultingText.Append(line);
            }

            StringBuilder stringToInsert = new StringBuilder();

            // strings that come before user-selected lines
            foreach (string beforeSelectedCode in StringResources.Instance.StringsBeforeSelectedCode)
            {
                stringToInsert.Append(originalIndent);
                stringToInsert.Append(beforeSelectedCode);
                stringToInsert.Append(Environment.NewLine);
            }

            stringToInsert.Append(resultingText);

            // strings that come after user-selected lines
            foreach (string afterSelectedCode in StringResources.Instance.StringsAfterSelectedCode)
            {
                stringToInsert.Append(originalIndent);
                stringToInsert.Append(afterSelectedCode);
                stringToInsert.Append(Environment.NewLine);
            }

            // Do replacement in Visual Studio text buffer
            earlierPoint.ReplaceText(laterPoint, stringToInsert.ToString(), (int)EnvDTE.vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
        }
    }
}
