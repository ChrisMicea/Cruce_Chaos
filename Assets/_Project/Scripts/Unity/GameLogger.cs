using System;
using System.IO;
using System.Text;
using UnityEngine;
using CruceChaos.Core.Game.Scoring;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Writes a full game history log to a text file on disk.
    ///
    /// The log file is written to Application.persistentDataPath, which is:
    ///   Linux/Mac : ~/.config/unity3d/<Company>/<Product>/
    ///   Windows   : %APPDATA%\..\LocalLow\<Company>\<Product>\
    ///   In-Editor : shown in the Console when the first match starts.
    ///
    /// Each match gets its own file named:
    ///   game_log_YYYY-MM-DD_HH-mm-ss.txt
    ///
    /// SCENE SETUP:
    ///   Add this component to the GameManager GameObject.
    ///   The GameManager calls the Log*() methods below at each game event.
    /// </summary>
    public class GameLogger : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, also mirrors every log line to the Unity Console.")]
        [SerializeField] private bool _mirrorToConsole = false;

        private string        _filePath;
        private StringBuilder _buffer = new StringBuilder();
        private int           _matchNumber  = 0;
        private int           _roundNumber  = 0;
        private int           _trickNumber  = 0;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _filePath = Path.Combine(Application.persistentDataPath, $"game_log_{timestamp}.txt");
            Debug.Log($"[GameLogger] Writing game history to: {_filePath}");
        }

        private void OnApplicationQuit()
        {
            Flush();
        }

        // ── Public logging API ────────────────────────────────────────────────

        public void LogMatchStart()
        {
            _matchNumber++;
            _roundNumber = 0;
            WriteLine();
            WriteLine($"╔══════════════════════════════════════════╗");
            WriteLine($"  MATCH {_matchNumber}  —  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteLine($"╚══════════════════════════════════════════╝");
        }

        public void LogRoundStart(int roundNum)
        {
            _roundNumber = roundNum;
            _trickNumber = 0;
            WriteLine();
            WriteLine($"┌─── Round {_roundNumber} ────────────────────────────────");
        }

        public void LogDeal(string humanHand, string aiHand, int deckCount)
        {
            WriteLine($"│  Deal:");
            WriteLine($"│    Your hand : {humanHand}");
            WriteLine($"│    AI hand   : {aiHand}");
            WriteLine($"│    Draw pile : {deckCount} cards");
        }

        public void LogBid(string playerName, int? bidValue)
        {
            if (bidValue.HasValue)
                WriteLine($"│  Bid  : {playerName} bids {bidValue.Value}");
            else
                WriteLine($"│  Bid  : {playerName} passes");
        }

        public void LogBidWinner(string playerName, int bid)
        {
            WriteLine($"│  Bid won by {playerName} at {bid}");
        }

        public void LogForcedBid(string playerName)
        {
            WriteLine($"│  All passed — {playerName} forced to bid 1");
        }

        public void LogTrumpDeclared(string playerName, string suit)
        {
            WriteLine($"│  Trump: {playerName} declares {suit}");
        }

        public void LogTrickStart(int trickNum, string leaderName)
        {
            _trickNumber = trickNum;
            WriteLine($"│");
            WriteLine($"│  ── Trick {_trickNumber} (led by {leaderName}) ──");
        }

        public void LogCardPlayed(string playerName, string card)
        {
            WriteLine($"│    {playerName,-12} plays  {card}");
        }

        public void LogMarriage(string playerName, string suit, int bonus)
        {
            WriteLine($"│    {playerName,-12} announces marriage in {suit} (+{bonus})");
        }

        public void LogTrickResult(string winnerName, int points, int humanRunning, int aiRunning)
        {
            WriteLine($"│    → {winnerName} wins {points} pts  " +
                      $"[You: {humanRunning} / AI: {aiRunning}]");
        }

        public void LogDraw(string playerName, string card)
        {
            WriteLine($"│    {playerName,-12} draws  {card}");
        }

        public void LogRoundResult(
            int humanCardPts, int humanMarriagePts, int humanTotal,
            int aiCardPts,    int aiMarriagePts,    int aiTotal,
            int? winningBid,  bool humanWonBid,
            int humanVP,      int aiVP,
            int humanDelta,   int aiDelta,
            int humanMatchScore, int aiMatchScore)
        {
            WriteLine($"│");
            WriteLine($"│  Round {_roundNumber} Result:");
            WriteLine($"│    You : {humanCardPts} card pts + {humanMarriagePts} marriage = {humanTotal} total → {StandardRoundScorer.VictoryPointsFor(humanTotal)} raw VP");
            WriteLine($"│    AI  : {aiCardPts} card pts + {aiMarriagePts} marriage = {aiTotal} total → {StandardRoundScorer.VictoryPointsFor(aiTotal)} raw VP");

            if (winningBid.HasValue)
            {
                string bidder = humanWonBid ? "You" : "AI";
                WriteLine($"│    Bid : {bidder} bid {winningBid.Value}");
                int bidderVP   = StandardRoundScorer.VictoryPointsFor(humanWonBid ? humanTotal : aiTotal);
                string outcome = bidderVP >= winningBid.Value ? "made ✓" : "missed ✗";
                WriteLine($"│    Contract: {outcome}");
            }

            WriteLine($"│    Deltas  : You {humanDelta:+0;-0;0} / AI {aiDelta:+0;-0;0}");
            WriteLine($"│    Standings: You {humanMatchScore} – AI {aiMatchScore}");
            WriteLine($"└──────────────────────────────────────────────");
        }

        public void LogMatchOver(bool humanWins, int humanScore, int aiScore)
        {
            WriteLine();
            WriteLine($"╔══════════════════════════════════════════╗");
            string result = humanWins
                ? $"  YOU WIN THE MATCH!  {humanScore} – {aiScore}"
                : $"  AI WINS THE MATCH!  {humanScore} – {aiScore}";
            WriteLine(result);
            WriteLine($"╚══════════════════════════════════════════╝");
            Flush();
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void WriteLine(string line = "")
        {
            _buffer.AppendLine(line);
            if (_mirrorToConsole)
                Debug.Log(line);
        }

        /// <summary>Writes the buffer to disk. Called on quit and after each match.</summary>
        public void Flush()
        {
            if (_buffer.Length == 0) return;
            try
            {
                File.AppendAllText(_filePath, _buffer.ToString());
                _buffer.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameLogger] Failed to write log: {e.Message}");
            }
        }
    }
}
