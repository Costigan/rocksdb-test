using System;
using System.Collections.Generic;

namespace gov.nasa.arc.ccsds.core
{
    public class SwapFixingFrameFileSequence : FrameFileSequence
    {
        private byte[] _buf1;
        private byte[] _buf2;
        private int _fc1 = -1; // free
        private int _fc2 = -1; // free

        public SwapFixingFrameFileSequence(IEnumerable<string> filenames)
            : base(filenames)
        {
        }

        public override string DisplayName()
        {
            return _displayName ?? "Frame file sequence (fixes swapped frames)";
        }

        public override IEnumerable<byte[]> FrameIterator()
        {
            if (_buf1 == null) _buf1 = new byte[65536 + 12];
            if (_buf2 == null) _buf2 = new byte[65536 + 12];
            _fc1 = _fc2 = -1;

            foreach (var ff in Files)
            {
                _frameFile = ff;
                //Console.WriteLine(ff.Filename());
                ff.Open();
                while (Read(ff))
                {
                    if (_fc1 < 0 || _fc2 < 0) continue;
                    if (_fc1 == 0)
                    {
                        //Console.WriteLine(_fc2);
                        _fc2 = -1;
                        yield return _buf2;
                    }
                    else if (_fc2 == 0)
                    {
                        //Console.WriteLine(_fc1);
                        _fc1 = -1;
                        yield return _buf1;
                    }
                    else if (_fc1 < _fc2)
                    {
                        //Console.WriteLine(_fc1);
                        _fc1 = -1;
                        yield return _buf1;
                    }
                    else
                    {
                        //Console.WriteLine(_fc2);
                        _fc2 = -1;
                        yield return _buf2;
                    }
                }
                ff.Close();
                _frameFile = null;
            }
            if (_fc1 >= 0)
                yield return _buf1;
            if (_fc2 >= 0)
                yield return _buf2;
        }

        private bool Read(FrameFile ff)
        {
            if (_fc1 < 0)
            {
                if (!ff.Read(_buf1)) return false;
                _fc1 = FrameAccessor.FrameCount(_buf1);
                return true;
            }
            if (_fc2 < 0)
            {
                if (!ff.Read(_buf2)) return false;
                _fc2 = FrameAccessor.FrameCount(_buf2);
                return true;
            }
            throw new Exception("No free frame buffers");
        }
    }
}