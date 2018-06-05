////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  MetaAutomation (C) 2018 by Matt Griscom.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
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
    internal sealed class RemoveCheckStep
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4129;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("d870339a-2fc4-41fb-b39c-28d51979f3f6");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoveCheckStep"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private RemoveCheckStep(AsyncPackage package, OleMenuCommandService commandService)
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
        public static RemoveCheckStep Instance
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
            // Verify the current thread is the UI thread - the call to AddCommand in RemoveCheckStep's constructor requires
            // the UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new RemoveCheckStep(package, commandService);
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
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                EnvDTE.TextDocument activeTextDocument = (EnvDTE.TextDocument)this.GetDTE().ActiveDocument.Object("TextDocument");
                EnvDTE.TextSelection selection = activeTextDocument.Selection;

                this.SubstituteStringFromSelection_RemoveCheckSteps(activeTextDocument);
            }
            catch (InvalidOperationException)
            {
                VsShellUtilities.ShowMessageBox(this.package, "The code selection is unbalanced; please try again selecting in the correct lines of code for a complete set of one or more check steps, including balanced parens.", "Error in Text Selection",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(this.package, string.Format("Exception type='{0}', Message='{1}'", ex.GetType().ToString(), ex.Message), "Exception",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }


        /// <summary>
        /// Put the selected lines of code inside a check step.
        /// This method does all modification of the text in the editor, with one string substitution
        /// </summary>
        /// <param name="document"></param>
        /// <param name="selection"></param>
        private void SubstituteStringFromSelection_RemoveCheckSteps(EnvDTE.TextDocument document)
        {
            EnvDTE.EditPoint earlierPoint = null, laterPoint = null;
            EditingTools.Instance.GetEditPointsForLinesToCheck(document, out earlierPoint, out laterPoint);
            string indentSpaces = EditingTools.Instance.GetIndentSpaces(document);
            string linesNoTabs = earlierPoint.GetText(laterPoint).Replace("\t", indentSpaces);
            linesNoTabs = linesNoTabs.TrimEnd(new char[] { '\r', '\n' });

            string[] lines = linesNoTabs.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);


            Stack<int[]> checkStepOpenings = new Stack<int[]>();

            // use for loop instead of foreach because we might have to look with multiple lines
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                // Iterate through lines, pushing opening line(s) for Check.Step on stack in string[]
                if (LinesMatchCheckStepOpening(new string[] { lines[lineIndex] }))
                {
                    checkStepOpenings.Push(new int[] { lineIndex });
                }
                else if ((lineIndex + 1 < lines.Length) && LinesMatchCheckStepOpening(new string[] { lines[lineIndex], lines[lineIndex + 1] }))
                {
                    checkStepOpenings.Push(new int[] { lineIndex, lineIndex + 1 });
                    lineIndex++;
                }
                else if (this.LineMatchesCheckStepClosing(lines[lineIndex]))
                {
                    // Remove check step
                    int[] openingLines = checkStepOpenings.Pop();
                    lines = RemoveCheckStepDefinition(openingLines, lineIndex, lines, indentSpaces);
                }
            }

            StringBuilder resultingText = new StringBuilder();

            foreach (string line in lines)
            {
                if (line != null)
                {
                    resultingText.Append(line);
                    resultingText.Append(Environment.NewLine);
                }
            }

            earlierPoint.ReplaceText(laterPoint, resultingText.ToString(), (int)EnvDTE.vsEPReplaceTextOptions.vsEPReplaceTextAutoformat);
        }

        string[] RemoveCheckStepDefinition(int[] openingLines, int endLine, string[] lines, string indentWithSpaces)
        {
            int lastOpeningLine = -1;

            foreach (int lineToRemove in openingLines)
            {
                // remove opening line
                lines[lineToRemove] = null;

                if (lineToRemove >= lastOpeningLine)
                {
                    lastOpeningLine = lineToRemove + 1;
                }
            }

            // remove ending line
            lines[endLine] = null;

            for (int lineIndex = lastOpeningLine; lineIndex < endLine; lineIndex++)
            {
                if ((lines[lineIndex] != null) && (lines[lineIndex].Length >= indentWithSpaces.Length) && (lines[lineIndex].Substring(0, indentWithSpaces.Length) == indentWithSpaces))
                {
                    // un-indent line in check step statement
                    lines[lineIndex] = lines[lineIndex].Substring(indentWithSpaces.Length);
                }
            }

            return lines;
        }

        /// <summary>
        /// For a successful match, the one or two lines passed must include
        /// *the check step definition
        /// *the opening curly brace for the anonymous delegate definition
        /// *the name of the check step must be in the first line of the passed array
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        private bool LinesMatchCheckStepOpening(string[] lines)
        {
            bool returnValue = false;

            string tempLine = lines[0];
            int beginIndex = tempLine.IndexOf('"');
            int endIndex = tempLine.LastIndexOf('"');

            if ((beginIndex > -1) && (endIndex > -1))
            {
                tempLine = string.Concat(tempLine.Substring(0, beginIndex + 1), EditingTools.Instance.StandardCheckStepName, tempLine.Substring(endIndex));
                tempLine = tempLine.Replace(" ", "");
                tempLine = tempLine.Replace("\t", "");
                bool firstLineHasOpeningCurlyBrace = (tempLine.Substring(tempLine.Length - 1) == "{");

                if (firstLineHasOpeningCurlyBrace)
                {
                    // remove curly brace
                    tempLine = tempLine.Substring(0, tempLine.Length - 1);

                    if ((tempLine == EditingTools.Instance.StandardCheckStepInsertionFirstLine_ZeroWhitespace) && (lines.Length == 1))
                    {
                        returnValue = true;
                    }
                }
                else
                {
                    if (lines.Length == 2)
                    {
                        string temp2Line = lines[1];
                        temp2Line = temp2Line.Replace(" ", "");
                        temp2Line = temp2Line.Replace("\t", "");

                        if ((tempLine == EditingTools.Instance.StandardCheckStepInsertionFirstLine_ZeroWhitespace) && (temp2Line == "{"))
                        {
                            returnValue = true;
                        }
                    }
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Checks if a line matches closing line
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private bool LineMatchesCheckStepClosing(string line)
        {
            bool returnValue = false;
            string tempLine = line.Replace(" ", "");
            tempLine = tempLine.Replace("\t", "");

            string stringAfterNoWhitespace = StringResources.Instance.StringsAfterSelectedCode[0];
            stringAfterNoWhitespace = stringAfterNoWhitespace.Replace(" ", "");
            stringAfterNoWhitespace = stringAfterNoWhitespace.Replace("\t", "");

            if (tempLine == stringAfterNoWhitespace)
            {
                returnValue = true;
            }

            return returnValue;
        }
    }
}
