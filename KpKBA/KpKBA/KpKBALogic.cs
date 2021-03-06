﻿/*
 * Copyright 2016 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : KpKBA
 * Summary  : Device communication logic
 * 
 * Author   : Mikhail Zverkov
 * Created  : 2017
 * Modified : 2017
 * 
 * Description
 * KBA laser system communication notifications.
 */


using Scada.Comm.Devices.KpKBA;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Scada.Comm.Devices
{
    /// <summary>
    /// Device communication logic
    /// <para>Логика работы КП</para>
    /// </summary>
    public class KpKBALogic : KPLogic
    {
       
        private Config config;              // конфигурация соединения с KBA system
        private Laser laser;
        private bool fatalError;            // фатальная ошибка при инициализации КП
        private string state;               // состояние КП
        private bool writeState;            // вывести состояние КП
        private bool liveBit;              // бит жизни Kp
        private int isPrintingCount;        // количество сканирований для принятия решения о том что лазер не печатает
        private DateTime startSessionTime;      // Время начала сессии
        

        /// <summary>
        /// Конструктор
        /// </summary>
        public KpKBALogic(int number)
            : base(number)
        {
            CanSendCmd = true;
            ConnRequired = false;
            WorkState = WorkStates.Normal;
            
            config = new Config();
            fatalError = false;
            state = "";
            writeState = false;

            isPrintingCount = 5;
            startSessionTime = new DateTime();
           
            InitKPTags(new List<KPTag>()
            {
                new KPTag(0, Localization.UseRussian ? "---" : "---"),
                new KPTag(1, Localization.UseRussian ? "Актуальный номер рулона по первому ручь" : "Actual roll number UM1"),
                 new KPTag(2, Localization.UseRussian ? "Актуальный номер рулона по второму ручь" : "Actual roll number UM2"),
                  new KPTag(3, Localization.UseRussian ? "Счетчик печати" : "Print counter"),
                  new KPTag(4, Localization.UseRussian ? "Счетчик печати Ok" : "Print counter Ok"),
                   new KPTag(5, Localization.UseRussian ? "Печать активна" : "Print active"),
                   new KPTag(6, Localization.UseRussian ? "Печатает" : "Printing"),
                   new KPTag(7, Localization.UseRussian ? "Предуприждение" : "Alarm"),
                    new KPTag(8, Localization.UseRussian ? "Код предуприждения" : "Alarm code"),
                    new KPTag(9, Localization.UseRussian ? "Бит жизни Kp измняет состояние при опросе" : "Live bit Kp"),
                    new KPTag(10, Localization.UseRussian ? "Время опроса лазера (мс)" : "Time session (mc)")
            });
        }


        /// <summary>
        /// Загрузить конфигурацию соединения с KBA system
        /// </summary>
        private void LoadConfig()
        {
            string errMsg;
            fatalError = !config.Load(Config.GetFileName(AppDirs.ConfigDir, Number), out errMsg);

            if (fatalError)
            {
                state = Localization.UseRussian ? 
                    "соедининие с KBA невозможно" : 
                    "connecting to KBA is impossible";
                throw new Exception(errMsg);
            }
            else
            {
                state = Localization.UseRussian ? 
                    "Ожидание данных..." :
                    "Waiting for data...";
            }
        }

        

        /// <summary>
        /// Выполнить сеанс опроса КП
        /// </summary>
        public override void Session()
        {
            if (writeState)
            {
                WriteToLog("");
                WriteToLog(state);
                writeState = false;
            }

            kpStats.SessCnt++;

            if (config.CheckTimeSession)
                startSessionTime = DateTime.Now;





           

            SetCurData(1, laser.reqActualNum(1), 1);
            Thread.Sleep(config.ReqDelay);

            SetCurData(2, laser.reqActualNum(2), 1);
            Thread.Sleep(config.ReqDelay);

            StatusPack status = laser.getStatus();
            Thread.Sleep(config.ReqDelay);

            SetCurData(3, status.printCount, 1);
            SetCurData(4, status.okPrintCount, 1);
            SetCurData(5, Convert.ToDouble(status.printIsStarted), 1);

            if (!status.isPrinting && isPrintingCount > 0) {
                isPrintingCount--;
            }
            if(status.isPrinting){
                isPrintingCount = 5;
                SetCurData(6, Convert.ToDouble(status.isPrinting), 1);
            }

            if(!status.isPrinting && isPrintingCount<=0)
                SetCurData(6, Convert.ToDouble(status.isPrinting), 1);


            SetCurData(7, Convert.ToDouble(status.isAlarm), 1);
               SetCurData(8, Convert.ToDouble(status.alarmCode), 1);
            SetCurData(9, Convert.ToDouble(liveBit = !liveBit), 1);


            if (config.CheckTimeSession)
                SetCurData(10, Convert.ToDouble(DateTime.Now.Subtract(startSessionTime).Ticks/10000), 1);

           

        }



        /// <summary>
        /// Выполнить действия при запуске линии связи
        /// </summary>
        public override void OnCommLineStart()
        {
            writeState = true;
            LoadConfig();
           
            laser = new Laser(config.Host, config.Port);

        }

    }
}