using APIView;
using TestLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.IO;

namespace APIViewWebApp.Models
{
    public class DLL
    {
        public int ID { get; set; }

        [Required]
        [Display(Name = "DLL Path")]
        public string DllPath { get; set; }

        [Display(Name = "Display String")]
        public string DisplayString { get; set; }

        // test DLL: C:\Users\t-mcpat\Documents\azure-sdk-tools\artifacts\bin\TestLibrary\Debug\netcoreapp2.1\TestLibrary.dll
        public DLL()
        {
            this.DisplayString = "";
            this.DllPath = "null";
        }

        public DLL(string dllPath)
        {
            AssemblyAPIV assembly = null;
            foreach (AssemblyAPIV a in AssemblyAPIV.AssembliesFromFile(dllPath))
            {
                if (a.Name.Equals("TestLibrary"))
                    assembly = a;
            }
            this.DisplayString = assembly.ToString();
            this.DllPath = dllPath;
        }
    }
}
