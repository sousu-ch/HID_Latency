using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MDXSample
{


    /// <summary>
    /// メインフォーム
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            // クライアントサイズ（フレームを除いたウインドウサイズ）
            this.ClientSize = new Size(640, 100);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}