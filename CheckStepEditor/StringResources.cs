////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  MetaAutomation (C) 2018 by Matt Griscom.
//
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckStepEditor
{
    public class StringResources
    {

        private StringResources()
        {
        }

        private static StringResources m_Instance = null;

        public static StringResources Instance
        {
            get
            {
                if (StringResources.m_Instance == null)
                {
                    StringResources.m_Instance = new StringResources();
                }

                return StringResources.m_Instance;
            }
        }

        private string[] m_StringsBeforeSelectedCode;

        public string[] StringsBeforeSelectedCode
        {
            get
            {
                if (this.m_StringsBeforeSelectedCode == null)
                {
                    this.LoadStringDefaults();
                }

                return this.m_StringsBeforeSelectedCode;
            }
            private set { }
        }

        private string[] m_StringsAfterSelectedCode;

        public string[] StringsAfterSelectedCode
        {
            get
            {
                if (this.m_StringsAfterSelectedCode == null)
                {
                    this.LoadStringDefaults();
                }

                return this.m_StringsAfterSelectedCode;
            }
            private set { }
        }

        private void LoadStringDefaults()
        {
            m_StringsBeforeSelectedCode = new string[] { "Check.Step(\"Edit this string to make a unique descriptive name for the step in the check context.\", delegate", "{" };
            m_StringsAfterSelectedCode = new string[] { "});" };
        }
    }
}
