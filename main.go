package main

import (
	"fmt"
	"os"

	dgo "github.com/bwmarrin/discordgo"
	bolt "go.etcd.io/bbolt"
)

type OrcaBot struct {
	BotSession *dgo.Session
	DBSession  *bolt.DB
	Params     *StartupParams
}

type StartupParams struct {
	Token             string
	DBPath            string
	ZncaClientId      string
	ZncaUserAgent     string
	ZncaClientVersion string
	NinUserAgent      string
}

func main() {
	botToken, tokenExists := os.LookupEnv("DISCORD_ORCA_TOKEN")
	if !tokenExists {
		fmt.Println("Couldn't startup the bot - no bot token was found.")
	}

	zncaId, tokenExists := os.LookupEnv("ZNCA_ID")
	if !tokenExists {
		fmt.Println("Couldn't startup the bot - no ZNCA Client ID was found.")
	}

	params := StartupParams{
		Token:             botToken,
		DBPath:            "ORCA.db",
		ZncaClientId:      zncaId,
		ZncaUserAgent:     "ORCA/0.0.1-dev (+github.com/Quark064/ORCA)",
		ZncaClientVersion: "hio87-mJks_e9GNF", // 3.2.0
		NinUserAgent:      "Dalvik/2.1.0 (Linux; U; Android 12; Build/SP1A.210812.016)",
	}

	botState := OrcaBot{
		Params: &params,
	}

	err := initState(&botState)
	if err != nil {
		fmt.Println("Couldn't startup the bot - ", err)
		return
	}
}

func initState(orcaBot *OrcaBot) error {
	bot, err := dgo.New("Bot " + orcaBot.Params.Token)
	if err != nil {
		return err
	}
	orcaBot.BotSession = bot

	db, err := bolt.Open(orcaBot.Params.DBPath, 0600, nil)
	if err != nil {
		return err
	}
	orcaBot.DBSession = db

	db.Update(func(tx *bolt.Tx) error {
		tx.CreateBucketIfNotExists([]byte("SessionMessageId"))
		tx.CreateBucketIfNotExists([]byte("SessionCode"))

		return nil
	})

	return nil
}
