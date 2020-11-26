﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;

namespace wfca
{

    public sealed class ConsoleReadActivity : CodeActivity
    {
        // Define an activity input argument of type string
        public OutArgument<string> ReadLine { get; set; }

        // If your activity returns a value, derive from CodeActivity<TResult>
        // and return the value from the Execute method.
        protected override void Execute(CodeActivityContext context)
        {
            // Obtain the runtime value of the Text input argument
            //string text = context.GetValue(this.Text);
            context.SetValue(ReadLine, Console.ReadLine());
        }
    }
}