using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace MDXSample
{
    /// <summary>
    /// アプリケーション開始クラス
    /// </summary>
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // コントロール等を Windows XP のスタイルにします。
            // はずしてもかまいません。
            Application.EnableVisualStyles();

            // GDI+ を使用してコントロールのテキストを描画します。
            // はずしてもかまいません。
            Application.SetCompatibleTextRenderingDefault(false);

            // フォームとメインサンプルクラスを作成
            // using() で作成されたリソースは {} を外れると自動的に破棄される
            // ここでは frm と sample が対象
            using (MainForm frm = new MainForm())
            using (MainSample sample = new MainSample())
            {
                // アプリケーションの初期化
                if (sample.InitializeApplication(frm))
                {
                    // メインフォームを表示
                    frm.Show();

                    // フォームが作成されている間はループし続ける
                    while (frm.Created)
                    {
                        // メインループ処理を行う
                        sample.MainLoop();

                        // ＣＰＵがフル稼働しないようにちょっとだけ制限をかける。
                        // パフォーマンスを最大限にあげるなら「0」を指定、
                        // または「Thread.Sleep」をはずす。
                        //Thread.Sleep(1);

                        // イベントがある場合はその処理する
                        Application.DoEvents();
                    }
                }
                else
                {
                    // 初期化に失敗
                }
            }
        }
    }
}