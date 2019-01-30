﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BeeBot;
using Newtonsoft.Json.Bson;
using NUnit.Framework;
using Random.Org;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;


namespace BeeBot.Tests.Controllers
{
    [TestClass]
    public class HomeControllerTest
    {
        [Test]
        public void TestStringToTimeSpan()
        {
            var stringMinutes = "3:15";
            var stringHours = "14:03:10";

            var res = StringToTimeSpan(stringMinutes);

            Assert.AreEqual(res.Minutes, 3);
            Assert.AreEqual(res.Seconds, 15);
        }
        

        [Test]
        public void TestRandom()
        {


            var list = new List<string>();

            list.Add("BorgeJ");
            list.Add("Rob");
            list.Add("Tom");
            list.Add("Don");
            list.Add("Ricky");

            var randomlist = Randomize(list);

            foreach (var elem in randomlist)
            {
                Console.Write(elem);
            }

            Assert.AreNotEqual(list, randomlist);
        }

        private TimeSpan StringToTimeSpan(string time)
        {
            var format = "m\\:ss";
            var culture = CultureInfo.CurrentCulture;

            return TimeSpan.ParseExact(time, format, culture);
        }

        public static List<T> Randomize<T>(List<T> list)
        {
            List<T> randomizedList = new List<T>();
            try
            {
                Random.Org.Random rnd = new Random.Org.Random();
                while (list.Count > 0)
                {
                    int index = 0;
                    if (list.Count > 1)
                    {
                        index = rnd.Next(0, list.Count - 1); //pick a random item from the master list
                    }

                    randomizedList.Add(list[index]); //place it at the end of the randomized list
                    list.RemoveAt(index);
                }
            }
            catch (Exception e)
            {
            }
            return randomizedList;
        }
    }
}
