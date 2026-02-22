package requests

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"

	"golang.org/x/oauth2"
	"golang.org/x/oauth2/clientcredentials"
)

// Get a new Access Token to use with the ZNCA API.
func ZncaGetAccessToken(clientId string) (string, error) {
	oauthUrl := "https://nxapi-auth.fancy.org.uk/api/oauth/token"

	conf := &clientcredentials.Config{
		ClientID:  clientId,
		TokenURL:  oauthUrl,
		Scopes:    []string{"ca:gf", "ca:er", "ca:dr"},
		AuthStyle: oauth2.AuthStyleInParams,
	}

	token, err := conf.Token(context.Background())
	if err != nil {
		return "", err
	}

	return token.AccessToken, nil
}

type EncryptRequest struct {
	UserAgent   string
	ZncaToken   string
	ZncaVersion string
	NintendoUrl string
	CoralToken  *string // Nullable
	JsonBody    string
}

type EncryptResponse struct {
	EncryptedRequest []byte
}

func ZncaEncryptNinRequest(request *EncryptRequest) (*EncryptResponse, error) {
	encryptEndpoint := "https://nxapi-znca-api.fancy.org.uk/api/znca/encrypt-request"

	reqBody := map[string]any{
		"url":   request.NintendoUrl,
		"token": request.CoralToken,
		"data":  request.JsonBody,
	}

	jsonBody, err := json.Marshal(reqBody)
	if err != nil {
		return nil, err
	}

	req, err := http.NewRequestWithContext(
		context.Background(),
		http.MethodGet,
		encryptEndpoint,
		bytes.NewBuffer(jsonBody),
	)
	if err != nil {
		return nil, err
	}

	req.Header.Set("User-Agent", request.UserAgent)
	req.Header.Set("Authorization", "Bearer "+request.ZncaToken)
	req.Header.Set("Accept", "application/octet-stream")
	req.Header.Set("X-znca-Client-Version", request.ZncaVersion)

	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("Encrypt ZNCA endpoint returned a non-200 status: %s: %s", resp.Status, string(body))
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	return &EncryptResponse{
		EncryptedRequest: body,
	}, nil
}

type DecryptRequest struct {
	UserAgent       string
	ZncaToken       string
	ZncaVersion     string
	Base64CipherStr string
}

type DecryptResponse struct {
	DecryptedJson string
}

func ZncaDecryptNinResponse(request *DecryptRequest) (*DecryptResponse, error) {
	decryptEndpoint := "https://nxapi-znca-api.fancy.org.uk/api/znca/decrypt-request"

	reqBody := map[string]any{
		"data": request.Base64CipherStr,
	}

	jsonBody, err := json.Marshal(reqBody)
	if err != nil {
		return nil, err
	}

	req, err := http.NewRequestWithContext(
		context.Background(),
		http.MethodGet,
		decryptEndpoint,
		bytes.NewBuffer(jsonBody),
	)
	if err != nil {
		return nil, err
	}

	req.Header.Set("User-Agent", request.UserAgent)
	req.Header.Set("Authorization", "Bearer "+request.ZncaToken)
	req.Header.Set("Accept", "text/plain")
	req.Header.Set("X-znca-Client-Version", request.ZncaVersion)

	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("Decrypt ZNCA endpoint returned a non-200 status: %s: %s", resp.Status, string(body))
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	return &DecryptResponse{
		DecryptedJson: string(body),
	}, nil
}
