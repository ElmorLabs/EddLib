using System.Runtime.InteropServices;

namespace EddLib
{
    class EddWrapper
    {
        [DllImport("EddCppWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static bool edd_init(int index);

        [DllImport("EddCppWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static int edd_get_button_status(int index);

        [DllImport("EddCppWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static bool edd_send_fb(int index, IntPtr frame_buffer);

        [DllImport("EddCppWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public extern static bool edd_deinit(int index);

    }

    public class EddLib
    {
        public static List<EddDevice> GetAllDevices()
        {
            return new List<EddDevice>();
        }

    }
}