using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;
using System.IO;
using System.Diagnostics;

namespace SELDLA_G
{
    internal class Class1
    {
        public static void Main2()
        {
            using Context context = Context.Create(builder => builder.AllAccelerators());
            Debug.WriteLine("Context: " + context.ToString());

            Device d = context.GetPreferredDevice(preferCPU: false);
            Accelerator accelerator = d.CreateAccelerator(context);

            accelerator.PrintInformation();
            //a.Dispose();


            // Load the data.
            MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(10_000);

            // load / precompile the kernel
            Action<Index1D, Index1D, ArrayView<int>, ArrayView<int>> loadedKernel =
                accelerator.LoadAutoGroupedStreamKernel<Index1D, Index1D, ArrayView<int>, ArrayView<int>>(Kernel);

            // finish compiling and tell the accelerator to start computing the kernel
            loadedKernel((int)deviceOutput.Length, (int)deviceOutput.Length, deviceData.View, deviceOutput.View);

            // wait for the accelerator to be finished with whatever it's doing
            // in this case it just waits for the kernel to finish.
            accelerator.Synchronize();

            // moved output data from the GPU to the CPU for output to console
            int[] hostOutput = deviceOutput.GetAsArray1D();
            Console.WriteLine(hostOutput.Length);
            for (int i = 0; i < 50; i++)
            {
                Console.Write(hostOutput[i]);
                Console.Write(" ");
            }

            accelerator.Dispose();
            context.Dispose();
        }
        static void Kernel(Index1D i, Index1D j, ArrayView<int> data, ArrayView<int> output)
        {
            //output[i] = data[i % data.Length];
            output[i] = i + j;
        }

    }
}
