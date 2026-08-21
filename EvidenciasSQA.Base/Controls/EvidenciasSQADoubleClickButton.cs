using System.Windows.Forms;

namespace EvidenciasSQA.Base.Controls
{
    public class EvidenciasSQADoubleClickButton : Button
    {
        public EvidenciasSQADoubleClickButton()
        {
            SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        }
    }
}
