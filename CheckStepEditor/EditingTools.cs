////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  MetaAutomation (C) 2016 by Matt Griscom.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using EnvDTE;
using System;
using System.Text;

namespace CheckStepEditor
{
    public class EditingTools
    {
        private static EditingTools m_Instance = null;

        private EditingTools()
        {
            try
            {
                // initialize standard check step name from data
                string standardCheckStepInsertion = StringResources.Instance.StringsBeforeSelectedCode[0];
                int beginIndex = standardCheckStepInsertion.IndexOf('"');
                int endIndex = standardCheckStepInsertion.LastIndexOf('"');
                this.m_standardCheckStepName = standardCheckStepInsertion.Substring(beginIndex + 1, endIndex - beginIndex - 1);

                // initialize whitespace-free insertion line from data
                standardCheckStepInsertion = standardCheckStepInsertion.Replace(" ", "");
                m_standardCheckStepInsertionFirstLine_ZeroWhitespace = standardCheckStepInsertion.Replace("\t", "");
            }
            catch(Exception)
            {
            }
        }

        public static EditingTools Instance
        {
            get
            {
                if (EditingTools.m_Instance == null)
                {
                    EditingTools.m_Instance = new EditingTools();
                }

                return EditingTools.m_Instance;
            }
        }

        private string m_standardCheckStepName = string.Empty;

        public string StandardCheckStepName
        {
            get
            {
                return this.m_standardCheckStepName;
            }
        }

        private string m_standardCheckStepInsertionFirstLine_ZeroWhitespace = string.Empty;

        public string StandardCheckStepInsertionFirstLine_ZeroWhitespace
        {
            get
            {
                return this.m_standardCheckStepInsertionFirstLine_ZeroWhitespace;
            }
        }

        /// <summary>
        /// Get and position EditPoint objects to include all of original text that is selected, plus surrounding text for complete lines of text
        /// </summary>
        /// <param name="document"></param>
        /// <param name="earlierPoint"></param>
        /// <param name="laterPoint"></param>
        public void GetEditPointsForLinesToCheck(TextDocument document, out EditPoint earlierPoint, out EditPoint laterPoint)
        {
            TextSelection selection = document.Selection;

            // Reorder selection points as needed
            if (selection.IsActiveEndGreater)
            {
                selection.SwapAnchor();
            }

            // Get and position EditPoint objects to include all of original text that is selected, plus surrounding text for complete lines of text
            earlierPoint = selection.ActivePoint.CreateEditPoint();
            laterPoint = selection.AnchorPoint.CreateEditPoint();

            earlierPoint.StartOfLine();
            laterPoint.LineDown(1);
            laterPoint.StartOfLine();
        }

        public string GetIndentSpaces(TextDocument document)
        {
            StringBuilder indentSpaces = new StringBuilder();
            indentSpaces.Append(' ', document.TabSize);
            return indentSpaces.ToString();
        }
    }
}
