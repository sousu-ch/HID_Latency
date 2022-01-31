using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.DirectInput;
using Microsoft.DirectX.Direct3D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FTD2XX_NET;
using static FTD2XX_NET.FTDI;

namespace MDXSample
{
    /// <summary>
    /// メインサンプルクラス
    /// </summary>
    public class MainSample : IDisposable
    {
        //DLLのインポート
        [DllImport("winmm.dll")]
        static extern long timeGetTime();
        [DllImport("winmm.dll")]
        static extern void timeBeginPeriod(int x);
        [DllImport("winmm.dll")]
        static extern void timeEndPeriod(int x);
        [DllImport("kernel32.dll")]
        extern static long QueryPerformanceCounter(ref long x);
        [DllImport("kernel32.dll")]
        extern static long QueryPerformanceFrequency(ref long x);

      

        /// <summary>
        /// メインフォーム
        /// </summary>
        private MainForm _form = null;

        /// <summary>
        /// Direct3D デバイス
        /// </summary>
        private Microsoft.DirectX.Direct3D.Device _D3Ddevice = null;

        /// <summary>
        /// マウスデバイス
        /// </summary>
        private Microsoft.DirectX.DirectInput.Device _mouseDevice = null;

        /// <summary>
        /// ジョイスティックデバイス
        /// </summary>
        private Microsoft.DirectX.DirectInput.Device _joyDevice = null;

        /// <summary>
        /// キーボードデバイス
        /// </summary>
        private Microsoft.DirectX.DirectInput.Device _keyboardDevice = null;

        /// <summary>
        /// Direct3D 用フォント
        /// </summary>
        private Microsoft.DirectX.Direct3D.Font _font = null;

        /// <summary>
        /// FTDI入出力デバイス
        /// </summary>
        private FTDI ftdev = null;





        public int clickcount;
        public Stopwatch m_SelectStateTime;
        public long startTime;
        public long currentTime;
        public double button0Time;
        public double button1Time;
        public long QPFFreq;
        public bool FT232_pin_EN = true;//true:TXD=H,false:TXD=L
        public const double Button_DOWN_TO_UP_INTERVAL = 50;//入力検知から次の出力までのINTERVALは、DELAY_COUNTが加算されます
        public const double Button_UP_TO_DOWN_INTERVAL = 50;//入力検知から次の出力までのINTERVALは、DELAY_COUNTが加算されます
        public const long NONE_INPUT = 0;
        public const long KEY_INPUT = 1;
        public const long JOY_INPUT = 2;
        public const long MOUSE_INPUT = 3;
        public const long FTDI_INPUT = 4;
        public double DELAY_COUNT_MAX = 16;//DELAY_COUNTはこの値より小かつDELAY_COUNT_STEP刻みの値をとります。（加算ループ）
        public double DELAY_COUNT_STEP = 0.1;//応答時間が1ms未満のデバイスなどは、この値を小さくすると、より詳細に応答分布がわかります。
        public double DELAY_COUNT = 0;

        public uint writtenLength = 0;
        public uint readLength = 0;
        public byte pinState;
        public long INPUT_DEVICE = NONE_INPUT;//デバイスが押されたかで、離す判定を切り分けるためのフラグです

        // マウスのデータ構造体
        public struct ButtonPushLog
        {
            public double onTime;   // OFFからONまでにかかった時間
            public double offTime;   // ONからOFFまでにかかった時間

        }
        public ButtonPushLog data;

        public List<ButtonPushLog> button_push_log;


        public MainSample()
        {
            m_SelectStateTime = new Stopwatch();
            button_push_log = new List<ButtonPushLog>();
            button0Time = 0;
            button1Time = 0;
            QueryPerformanceFrequency(ref QPFFreq);


        }
        /// <summary>
        /// アプリケーションの初期化
        /// </summary>
        /// <param name="topLevelForm">トップレベルウインドウ</param>
        /// <returns>全ての初期化がＯＫなら true, ひとつでも失敗したら false を返すようにする</returns>
        /// <remarks>
        /// false を返した場合は、自動的にアプリケーションが終了するようになっている
        /// </remarks>
        public bool InitializeApplication(MainForm topLevelForm)
        {
            // フォームの参照を保持
            this._form = topLevelForm;

            // PresentParameters。デバイスを作成する際に必須
            // どのような環境でデバイスを使用するかを設定する
            PresentParameters pp = new PresentParameters();

            // ウインドウモードなら true、フルスクリーンモードなら false を指定
            pp.Windowed = true;

            // スワップ効果。とりあえず「Discard」を指定。
            pp.SwapEffect = SwapEffect.Discard;

            // ウインドウモードの場合は「Default」か「Immediate」を指定。
            // Default は 垂直帰線間隔を待機します(一定の間隔で画面更新)
            // Immediate は 待機せず直ちに更新します
            pp.PresentationInterval = PresentInterval.Default;


            // 実際にデバイスを作成します。
            // 常に最高のパフォーマンスで作成を試み、
            // 失敗したら下位パフォーマンスで作成するようにしている。
            try
            {
                // ハードウェアによる頂点処理、ラスタライズを行う
                // 最高のパフォーマンスで処理を行えます。
                // ビデオカードによっては実装できない処理が存在します。
                this._D3Ddevice = new Microsoft.DirectX.Direct3D.Device(0, Microsoft.DirectX.Direct3D.DeviceType.Hardware, topLevelForm.Handle,
                    CreateFlags.HardwareVertexProcessing, pp);

                // フォントデータの構造体を作成
                this._font = new Microsoft.DirectX.Direct3D.Font(
                    _D3Ddevice,
                    30, 0,
                    FontWeight.Normal,
                    0,
                    false,
                    CharacterSet.ShiftJIS,
                    Precision.Default,
                    FontQuality.ClearType,
                    PitchAndFamily.DefaultPitch,
                    "MS Gothic"
                    );                // フォントを作成

            }
            catch (DirectXException ex)
            {
                MessageBox.Show(ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;

            }





            // ジョイスティックデバイスの初期化
            try
            {
                //create joystick device. 
                foreach (
                    DeviceInstance di in
                    Microsoft.DirectX.DirectInput.Manager.GetDevices(
                        DeviceClass.GameControl,
                        EnumDevicesFlags.AttachedOnly))
                {
                    this._joyDevice = new Microsoft.DirectX.DirectInput.Device(di.InstanceGuid);
                    break;
                }

                if(null != this._joyDevice)
                {

                    //Set joystick axis ranges. 
                    foreach (DeviceObjectInstance doi in this._joyDevice.Objects)
                    {
                        if ((doi.ObjectId & (int)DeviceObjectTypeFlags.Axis) != 0)
                        {
                            this._joyDevice.Properties.SetRange(
                                ParameterHow.ById,
                                doi.ObjectId,
                                new InputRange(-5000, 5000));
                        }
                    }

                    //Set joystick axis mode absolute. 
                    this._joyDevice.Properties.AxisModeAbsolute = true;

                    //set cooperative level. 
                    this._joyDevice.SetCooperativeLevel(topLevelForm,
                        CooperativeLevelFlags.NonExclusive | CooperativeLevelFlags.Background);
                }
            }
            catch (DirectXException ex)
            {
                MessageBox.Show(ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (null != this._joyDevice)
            {
                try
                {
                    // キャプチャするデバイスを取得
                    this._joyDevice.Acquire();
                    //this._mouseDevice.Acquire();
                }
                catch (DirectXException)
                {
                }
            }

            // キーボードデバイスの初期化
            try
            {
                // キーボードデバイスの作成
                this._keyboardDevice = new Microsoft.DirectX.DirectInput.Device(SystemGuid.Keyboard);

                // 協調レベルの設定
                this._keyboardDevice.SetCooperativeLevel(topLevelForm,
                    CooperativeLevelFlags.NonExclusive | CooperativeLevelFlags.Background);
            }
            catch (DirectXException ex)
            {
                MessageBox.Show(ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                // キャプチャするデバイスを取得
                this._keyboardDevice.Acquire();
            }
            catch (DirectXException)
            {
            }


            //// マウスデバイスの初期化
            try
            {
                // マウスデバイスの作成
                this._mouseDevice = new Microsoft.DirectX.DirectInput.Device(SystemGuid.Mouse);
                //    エラー	1	'Device' は、'Microsoft.DirectX.DirectInput.Device' と 'Microsoft.DirectX.Direct3D.Device'' 間のあいまいな参照です。	D:\prog\C#\MousePDclickD3D\MainSample.cs	99	41	MousePDclickD3D

                // 協調レベルの設定
                this._mouseDevice.SetCooperativeLevel(topLevelForm,
                    CooperativeLevelFlags.NonExclusive | CooperativeLevelFlags.Background);
            }
            catch (DirectXException ex)
            {
                MessageBox.Show(ex.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            //// バッファサイズを指定
            this._mouseDevice.Properties.BufferSize = 16;

            try
            {
                // キャプチャするデバイスを取得
                this._mouseDevice.Acquire();
            }
            catch (DirectXException)
            {
            }


            //FTDIのBit Bang Mode 初期化
            ftdev = new FTDI();
            ftdev.OpenByIndex(0);
            ftdev.SetBitMode(0x01, FTDI.FT_BIT_MODES.FT_BIT_MODE_ASYNC_BITBANG);
            ftdev.SetBaudRate(12000000);
            ftdev.SetLatency(10);

            return true;
        }

        /// <summary>
        /// H出力からキー押下までの時間（前回タイマースタートからの経過時間）を記録する。
        /// その後IOにL出力処理して（ソレノイドOFF)、次のタイマーをスタートする
        /// </summary>
        public void ButtonUp()
        {

            //前回のButtonDown指示（FTDI のIO をH出力)から入力デバイスからのキー押下されるまでの時間を記録する
            //QueryPerformanceCounterは周波数で割ると時間が出る
            QueryPerformanceCounter(ref currentTime);
            button0Time = ((currentTime - startTime) * 1000 / (double)QPFFreq);

            //取得した生データを構造体に代入するリストに入れる処理はButtonUP()の中で実行する
            //入れるのはクリックまでにかかった時間
            //data.onTime = button0Time - 8;
            data.onTime = button0Time;

            //Button_DOWN_TO_UP_INTERVAL。ソレノイド熱くなるので、短めにしたほうがいいかもしれない
            QueryPerformanceCounter(ref startTime);
            do
            {
                QueryPerformanceCounter(ref currentTime);
                button0Time = ((currentTime - startTime) * 1000 / (double)QPFFreq);
                //Application.DoEvents();
            } while (button0Time < Button_DOWN_TO_UP_INTERVAL + DELAY_COUNT);

            //FTDI の IOボードにより出力を最下位ビットにLを出力する。
            //WriteからWriteまでの時間をオシロで確認したが、1ms以上間隔をあけた場合、
            //ワースト誤差は約0.5msが見込めるようだった。環境依存があるかもしれないので注意すること。
            FT232_pin_EN = false;
            byte[] wbuf = { 0x00 };
            ftdev.Write(wbuf, wbuf.Length, ref writtenLength);

            //時間ぶれを防ぐため、PIN出力後直ちにカウントスタートする。
            QueryPerformanceCounter(ref startTime);


            return;
        }

        /// <summary>
        /// L出力からキー入力開放までの時間（前回タイマースタートからの経過時間）を記録する。
        /// その後IOにH出力処理（ソレノイドON)して、次のタイマーをスタートする
        /// </summary>
        public void ButtonDown()
        {
            //クリック回数を記録しておく
            //測定時は、最初の１回は開始までの時間が入ってしまうことがある。このためCSV見て最初の１回を削除する必要がある。運用で回避かな。
            clickcount += 1;

            //前回のButtonUP指示（FTDI のIO をL出力)から入力デバイスからのキー入力解放されるまでの時間を記録する
            //QueryPerformanceCounterは周波数で割ると時間が出る。
            QueryPerformanceCounter(ref currentTime);
            button0Time = ((currentTime - startTime) * 1000 / (double)QPFFreq);//floatにしたら桁落ちで1msより精度が出なかったので注意

            //取得した生データを構造体に代入する。
            //入れるのはキー入力解放までにかかった時間
            //data.offTime = button0Time - 2.7;
            data.offTime = button0Time;

            //生データをリストに代入する
            button_push_log.Add(data);


            //テキストの描画を一気にやる
            this._D3Ddevice.BeginScene();
            this._D3Ddevice.Clear(ClearFlags.Target, Color.FromArgb(255, 255, 255), 1.0f, 0);//白背景
            this._font.DrawText(null, clickcount + "回目 " + "\r\n" +"押: " + (data.onTime).ToString("000.000") + "[msec] " + "\r\n" + "離: " + (data.offTime).ToString("000.000") + "[msec] " , 0, 0, Color.Black);
            this._D3Ddevice.EndScene();
            //即時描画しちゃう。ここで垂直同期を待つ場合は、直後のINTERVALとこの描画部分を順番を入れ替えること。
            this._D3Ddevice.Present();

            //Button_UP_TO_DOWN_INTERVALだけ待つ。ソレノイドが熱くなるので、長めに待ってもいいかもしれない。
            QueryPerformanceCounter(ref startTime);
            do
            {
                QueryPerformanceCounter(ref currentTime);
                button0Time = ((currentTime - startTime) * 1000 / (double)QPFFreq);//floatにしたら桁落ちで1msより精度が出なかったので注意
                //Application.DoEvents();
            } while (button0Time < Button_UP_TO_DOWN_INTERVAL + DELAY_COUNT);

            //入力検出からIO出力開始までのDELAYを0 ot MAX[ms]の間で変化させる。
            //これを入れないと、USBのポーリング周期と入力周期が同期してしまい、測定値がランダム入力した場合の応答時間と乖離してしまう。
            DELAY_COUNT += DELAY_COUNT_STEP;
            if (DELAY_COUNT >= DELAY_COUNT_MAX)
            {
                DELAY_COUNT = 0;
            }

            //FTDI の IOボードにより出力を最下位ビットにHを出力する。
            //WriteからWriteまでの時間をオシロで確認したが、1ms以上間隔をあけた場合、
            //ワースト誤差は約0.5msが見込めるようだった。環境依存があるかもしれないので注意すること。
            FT232_pin_EN = true;
            byte[] wbuf = { 0x01 };//最下位ビットはTXD。ほかのピンに変えたかったらここをいじる
            ftdev.Write(wbuf, wbuf.Length, ref writtenLength);

            //時間ぶれを防ぐため、PIN出力後直ちにカウントスタートする。
            QueryPerformanceCounter(ref startTime);

            return;
        }



        public void LogOutput()
        {
            StreamWriter writer = new StreamWriter("data.csv");
            foreach (ButtonPushLog data in button_push_log)
            {
                //出力文字列作成
                //文字列出力
                writer.WriteLine(data.onTime + "," + data.offTime);
            }
            writer.Close();
            QueryPerformanceCounter(ref startTime);
            do
            {
                QueryPerformanceCounter(ref currentTime);
                button0Time = ((currentTime - startTime) * 1000 / (double)QPFFreq);
                Application.DoEvents();
            } while (button0Time < 1000);//何度も書きだすのを防ぐため、1秒待つ

            return;
        }

        /// <summary>
        /// メインループ処理
        /// </summary>
        public void MainLoop()
        {
            timeBeginPeriod(1);

            //keyboardポーリング処理ここから
            KeyboardState keystate = null;
            try
            {
                if (null != this._keyboardDevice)
                {
                    // 押されたキーをキャプチャ
                    keystate = this._keyboardDevice.GetCurrentKeyboardState();
                }
            }
            catch (DirectXException)
            {
                try
                {
                    if (null != this._keyboardDevice)
                    {
                        // キャプチャするデバイスを取得
                        this._keyboardDevice.Acquire();

                        // 押されたキーをキャプチャ
                        keystate = this._keyboardDevice.GetCurrentKeyboardState();
                    }
                }
                catch (DirectXException)
                {
                }
            }
            //keyboardポーリングここまで

            //Joystickポーリング処理ここから
            JoystickState joystate;

            int[] joybuttonstate = new int[10];//10ボタンを想定しておく
            try
            {
                if (null != this._joyDevice)
                {
                    // 押されたボタンをキャプチャ
                    joystate = this._joyDevice.CurrentJoystickState;
                    joybuttonstate[0] = joystate.GetButtons()[0];//他のボタンも取りたいときはここをいじる
                }
            }
            catch (DirectXException)
            {
                try
                {
                    if (null != this._joyDevice)
                    {
                        // キャプチャするデバイスを取得
                        this._joyDevice.Acquire();

                        // 押されたキーをキャプチャ
                        joystate = this._joyDevice.CurrentJoystickState;
                        joybuttonstate[0] = joystate.GetButtons()[0];//他のボタンも取りたいときはここをいじる
                    }
                }
                catch (DirectXException)
                {
                }
            }
            //Joystickポーリングここまで

            //マウスポーリング処理ここから
            MouseState mouseState;
            int[] mousebuttonstate = new int[10];//10ボタンを想定しておく
            try
            {
                if (null != this._mouseDevice)
                {
                    // 押されたボタンをキャプチャ
                    mouseState = this._mouseDevice.CurrentMouseState;
                    mousebuttonstate[0] = mouseState.GetMouseButtons()[0];//他のボタンも取りたいときはここをいじる
                }
            }
            catch (DirectXException)
            {
                try
                {
                    if (null != this._mouseDevice)
                    {
                        // キャプチャするデバイスを取得
                        this._mouseDevice.Acquire();

                        // 押されたキーをキャプチャ
                        mouseState = this._mouseDevice.CurrentMouseState;
                        mousebuttonstate[0] = mouseState.GetMouseButtons()[0];//他のボタンも取りたいときはここをいじる
                    }
                }
                catch (DirectXException)
                {
                }
            }
            //マウスポーリングここまで

            //FTDIのIOポーリング処理
            //ftdev.GetPinStates(ref pinState);


            //入力判定ここから
            if (FT232_pin_EN == true)
            {
                if (keystate != null)
                {
                    if (keystate[Key.H])//読み取りたいキーを決めている。キーを変更する場合、リリース側も変更する必要がある
                    {
                        //BUTTON UP処理
                        ButtonUp();
                        INPUT_DEVICE = KEY_INPUT;
                    }
                }
                if (joybuttonstate[0] > 0)//読み取りたいキーを決めている。キーを変更する場合、リリース側も変更する必要がある
                {
                    //BUTTON UP処理
                    ButtonUp();
                    INPUT_DEVICE = JOY_INPUT;

                }
                else if (mousebuttonstate[0] > 0)//読み取りたいキーを決めている。キーを変更する場合、リリース側も変更する必要がある
                {
                    //BUTTON UP処理
                    ButtonUp();
                    INPUT_DEVICE = MOUSE_INPUT;

                }
                //FTDIのIOポーリングテスト用
                if ((pinState & 0x02) == 0x02)
                {
                    ButtonUp();
                    INPUT_DEVICE = FTDI_INPUT;

                }


            }
            else
            {

                if (keystate[Key.H] == false && INPUT_DEVICE == KEY_INPUT)
                {
                    //BUTTON DOWN処理
                    ButtonDown();
                    INPUT_DEVICE = NONE_INPUT;
                }
                else if (joybuttonstate[0] == 0 && INPUT_DEVICE == JOY_INPUT)
                {
                    //BUTTON UP処理
                    ButtonDown();
                    INPUT_DEVICE = NONE_INPUT;
                }
                else if (mousebuttonstate[0] == 0 && INPUT_DEVICE == MOUSE_INPUT)
                {
                    //BUTTON UP処理
                    ButtonDown();
                    INPUT_DEVICE = NONE_INPUT;
                }
                //FTDIのIOポーリングテスト用
                if ((pinState & 0x02) != 0x02 && INPUT_DEVICE == FTDI_INPUT)
                {
                    ButtonDown();
                }


            }

            if (keystate != null)
            {
                if (keystate[Key.C])
                {
                    //書き出しの処理
                    LogOutput();
                }
            }

            //入力判定ここまで


            timeEndPeriod(1); 
        }

        /// <summary>
        /// リソースの破棄をするために呼ばれる
        /// </summary>
        public void Dispose()
        {
            // Direct3D デバイスのリソース解放
            if (this._D3Ddevice != null)
            {
                this._D3Ddevice.Dispose();
            }
            // マウスデバイスの解放
            if (this._mouseDevice != null)
            {
                this._mouseDevice.Dispose();
            }
            // キーボードティックデバイスの解放
            if (this._keyboardDevice != null)
            {
                this._keyboardDevice.Dispose();
            }
            // ジョイスティックデバイスの解放
            if (this._joyDevice != null)
            {
                this._joyDevice.Dispose();
            }
            //ftdev.Close();//どこかでCloseさせようと思ったが、アプリケーション終了でCloseされるっぽいので、放置した

        }
    }
}
