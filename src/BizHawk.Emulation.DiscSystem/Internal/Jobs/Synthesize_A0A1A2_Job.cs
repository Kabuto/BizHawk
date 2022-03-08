using System;
using System.Collections.Generic;

//TODO - generate correct Q subchannel CRC

namespace BizHawk.Emulation.DiscSystem
{
	/// <summary>
	/// Synthesizes RawTCOEntry A0 A1 A2 from the provided information.
	/// This might be reused by formats other than CUE later, so it isn't directly associated with that
	/// </summary>
	internal class Synthesize_A0A1A2_Job
	{
		private readonly int IN_FirstRecordedTrackNumber;

		private readonly int IN_LastRecordedTrackNumber;

		private readonly SessionFormat IN_Session1Format;

		private readonly int IN_LeadoutTimestamp;

		/// <param name="firstRecordedTrackNumber">"First Recorded Track Number" value for TOC (usually 1)</param>
		/// <param name="lastRecordedTrackNumber">"Last Recorded Track Number" value for TOC</param>
		/// <param name="session1Format">The session format for this TOC</param>
		/// <param name="leadoutTimestamp">The absolute timestamp of the lead-out track</param>
		public Synthesize_A0A1A2_Job(
			int firstRecordedTrackNumber,
			int lastRecordedTrackNumber,
			SessionFormat session1Format,
			int leadoutTimestamp)
		{
			IN_FirstRecordedTrackNumber = firstRecordedTrackNumber;
			IN_LastRecordedTrackNumber = lastRecordedTrackNumber;
			IN_Session1Format = session1Format;
			IN_LeadoutTimestamp = leadoutTimestamp;
		}

		/// <summary>appends the new entries to the provided list</summary>
		/// <exception cref="InvalidOperationException"><see cref="IN_Session1Format"/> is <see cref="SessionFormat.None"/> or a non-member</exception>
		public void Run(List<RawTOCEntry> entries)
		{
			//NOTE: entries are inserted at the beginning due to observations of CCD indicating they might need to be that way
			//Since I'm being asked to synthesize them here, I guess I can put them in whatever order I want, can't I?

			SubchannelQ sq = default;

			//ADR (q-Mode) is necessarily 0x01 for a RawTOCEntry
			const int kADR = 1;
			const int kUnknownControl = 0;

			sq.SetStatus(kADR, (EControlQ)kUnknownControl);

			//first recorded track number:
			sq.q_index.BCDValue = 0xA0;
			sq.ap_min.DecimalValue = IN_FirstRecordedTrackNumber;
			switch(IN_Session1Format)
			{
				//TODO these probably shouldn't be decimal values
				case SessionFormat.Type00_CDROM_CDDA: sq.ap_sec.DecimalValue = 0x00; break;
				case SessionFormat.Type10_CDI: sq.ap_sec.DecimalValue = 0x10; break;
				case SessionFormat.Type20_CDXA: sq.ap_sec.DecimalValue = 0x20; break;
				default: throw new InvalidOperationException("Invalid Session1Format");
			}
			sq.ap_frame.DecimalValue = 0;

			entries.Insert(0, new RawTOCEntry { QData = sq });

			//last recorded track number:
			sq.q_index.BCDValue = 0xA1;
			sq.ap_min.DecimalValue = IN_LastRecordedTrackNumber;
			sq.ap_sec.DecimalValue = 0;
			sq.ap_frame.DecimalValue = 0;

			entries.Insert(1, new RawTOCEntry { QData = sq });

			//leadout:
			sq.q_index.BCDValue = 0xA2;
			sq.AP_Timestamp = IN_LeadoutTimestamp;

			entries.Insert(2, new RawTOCEntry { QData = sq });
		}
	}
}