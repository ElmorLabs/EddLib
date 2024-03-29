﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EddLib
{
    public class EddDevice
    {
        public enum EddButton
        {
            Button1, Button2, Button3
        };

        public delegate void ButtonPressedEventHandler(int device_index, List<EddButton> button_list);
        public event ButtonPressedEventHandler? ButtonPressed;

        public delegate void DisconnectedEventHandler(int device_index);
        public event DisconnectedEventHandler? Disconnected;

        private void DeviceButtonCallback(int index, int button_status)
        {
            List<EddButton> button_list = new List<EddButton>();

            if ((button_status & 0x01) == 0)
            {
                button_list.Add(EddButton.Button1);
            }
            if ((button_status & 0x02) == 0)
            {
                button_list.Add(EddButton.Button2);
            }
            if ((button_status & 0x04) == 0)
            {
                button_list.Add(EddButton.Button3);
            }

            if (button_list.Count > 0)
            {
                ButtonPressed?.Invoke(index, button_list);
            }
        }

        public string Name { get; private set; } = string.Empty;
        public Guid Guid { get; private set; } = Guid.Empty;

        private int DeviceIndex;

        public EddDevice(int index)
        {
            DeviceIndex = index;
        }

        private EddWrapper.ButtonCallback? _buttonCallback;

        public bool Init()
        {
            try
            {
                _buttonCallback = new EddWrapper.ButtonCallback(DeviceButtonCallback);
                bool result = EddWrapper.edd_init(DeviceIndex, _buttonCallback);
                return result;
            }
            catch
            {
                return false;
            }
        }

        public bool DeInit()
        {
            try
            {
                return EddWrapper.edd_deinit(DeviceIndex);
            }
            catch
            {
                return false;
            }
        }

        // https://codereview.stackexchange.com/questions/175685/parsing-windows-bitmap-header-information
        public bool UpdateFramebuffer(MemoryStream ms)
        {

            byte[]? bitmap_data = null;
            byte[] oled_fb = new byte[128 * 64 / 8];

            uint pixel_offset = 0;
            uint header_size = 0;
            uint pixel_width = 0;
            uint pixel_height = 0;
            ushort pixel_depth = 0;
            uint row_size = 0;
            uint padding_bits = 0;
            uint padding_bytes = 0;
            uint width_bytes = 0;
            uint pixels = 0;

            try
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    br.BaseStream.Seek(10, SeekOrigin.Begin);
                    pixel_offset = br.ReadUInt32();//14
                    header_size = br.ReadUInt32();//18
                    pixel_width = br.ReadUInt32();//22
                    pixel_height = br.ReadUInt32();//26
                    br.ReadUInt16();//28
                    pixel_depth = br.ReadUInt16();//30
                    row_size = (((pixel_depth * pixel_width) + 31) / 32) * 4;
                    padding_bits = row_size * 8 - ((pixel_width * pixel_depth));
                    padding_bytes = row_size - ((pixel_width * pixel_depth) / 8);
                    width_bytes = (pixel_width * pixel_depth) / 8;
                    pixels = pixel_height * pixel_width;

                    if (pixel_width != 128 || pixel_height != 64 || pixel_depth != 1)
                    {
                        return false;
                    }

                    br.BaseStream.Seek(pixel_offset, SeekOrigin.Begin);
                    bitmap_data = new byte[pixel_width * pixel_height * pixel_depth / 8];

                    for(int y = 0; y < pixel_height; y++)
                    {
                        // Copy row
                        byte[] row = br.ReadBytes((int)width_bytes);
                        Array.Copy(row, 0, bitmap_data, y * width_bytes, width_bytes);

                        // Discard stride
                        br.ReadBytes((int)padding_bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            if(bitmap_data == null)
            {
                return false;
            }

            for (int y = 0; y < pixel_height; y++)
            {
                for (int x = 0; x < pixel_width; x++)
                {
                    int byte_index = ((int)pixel_height - y - 1) * (int)pixel_width / 8 + x / 8;
                    int bit_index = 7 - (x - (x / 8) * 8);

                    bool pixel_value = (bitmap_data[byte_index] & (1 << bit_index)) != 0;

                    OLED_SetPixel(ref oled_fb, x, y, pixel_value);
                }
            }

            GCHandle pinned_array = GCHandle.Alloc(oled_fb, GCHandleType.Pinned);
            IntPtr fb_ptr = pinned_array.AddrOfPinnedObject();

            try
            {
                bool result = EddWrapper.edd_send_fb(DeviceIndex, fb_ptr);
                return result;
            }
            catch
            {
                return false;
            }
            finally
            {
                pinned_array.Free();
            }
        }


        byte[] frameBuffer = new byte[128 * 64 / 8];
        public void SetBufferPixel(int x, int y, bool value)
        {
            OLED_SetPixel(ref frameBuffer, x, y, value);
        }

        public void ClearBuffer()
        {
            frameBuffer = new byte[128 * 64 / 8];
        }

        public bool FlushBuffer()
        {
            GCHandle pinned_array = GCHandle.Alloc(frameBuffer, GCHandleType.Pinned);
            IntPtr fb_ptr = pinned_array.AddrOfPinnedObject();

            try
            {
                bool result = EddWrapper.edd_send_fb(DeviceIndex, fb_ptr);
                return result;
            }
            catch
            {
                return false;
            }
            finally
            {
                pinned_array.Free();
            }
        }


        private void OLED_SetPixel(ref byte[] oled_fb, int x, int y, bool value)
        {
            int col = x;
            int row = y / 8;

            if (value)
            {
                oled_fb[128 * row + col] |= (byte)(1 << (y - row * 8));
            }
            else
            {
                oled_fb[128 * row + col] &= (byte)~(1 << (y - row * 8));
            }
        }
    }
}
