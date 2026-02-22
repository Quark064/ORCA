package requests

import (
	"bytes"
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
)

type LoginURLResult struct {
	AuthVerifier string
	URL          string
}

// Generates a unique Login URL for a user to authenticate with their Nintendo Account.
func GenerateSignInURL() *LoginURLResult {
	authStateBytes := make([]byte, 36)
	authVerifierBytes := make([]byte, 32)

	rand.Read(authStateBytes)
	rand.Read(authVerifierBytes)

	authState := base64.RawURLEncoding.EncodeToString(authStateBytes)
	authVerifier := base64.RawURLEncoding.EncodeToString(authVerifierBytes)

	// Compute SHA256 hash of verifier for challenge.
	challengeBytes := sha256.Sum256([]byte(authVerifier))
	authChallenge := base64.RawURLEncoding.EncodeToString(challengeBytes[:])

	base, _ := url.Parse("https://accounts.nintendo.com/connect/1.0.0/authorize")
	q := base.Query()

	q.Set("state", authState)
	q.Set("redirect_uri", "npf71b963c1b7b6d119://auth")
	q.Set("client_id", "71b963c1b7b6d119")
	q.Set("scope", "openid user user.birthday user.screenName")
	q.Set("response_type", "session_token_code")
	q.Set("session_token_code_challenge", authChallenge)
	q.Set("session_token_code_challenge_method", "S256")
	q.Set("theme", "login_form")

	base.RawQuery = q.Encode()

	return &LoginURLResult{
		authVerifier,
		base.String(),
	}
}

type SessionTokenRequest struct {
	UserAgent    string
	TokenCode    string
	AuthVerifier string
}

type SessionTokenResponse struct {
	Code         string `json:"code"`
	SessionToken string `json:"session_token"`
}

func GetSessionToken(request *SessionTokenRequest) (*SessionTokenResponse, error) {
	sessionTokenURL := "https://accounts.nintendo.com/connect/1.0.0/api/session_token"

	form := url.Values{}
	form.Set("client_id", "71b963c1b7b6d119")
	form.Set("session_token_code", request.TokenCode)
	form.Set("session_token_code_verifier", request.AuthVerifier)

	// Create HTTP request.
	req, err := http.NewRequestWithContext(context.Background(), http.MethodPost, sessionTokenURL, bytes.NewBufferString(form.Encode()))
	if err != nil {
		return nil, err
	}

	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	req.Header.Set("Accept", "application/json")
	req.Header.Set("User-Agent", request.UserAgent)
	req.Header.Set("Host", "accounts.nintendo.com")
	req.Header.Set("Connection", "Keep-Alive")

	// Perform request.
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	// Check if the request was successful.
	if resp.StatusCode != 200 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("Session token endpoint returned non-200 status: %s: %s", resp.Status, string(body))
	}

	// Parse and return result.
	var tokenResp SessionTokenResponse
	err = json.NewDecoder(resp.Body).Decode(&tokenResp)
	if err != nil {
		return nil, err
	}

	return &tokenResp, nil
}

type SecondaryTokenRequest struct {
	UserAgent    string
	SessionToken string
}

type SecondaryTokenResponse struct {
	AccessToken string `json:"access_token"`
	IdToken     string `json:"id_token"`
	ExpiresIn   int    `json:"expires_in"`
}

func GetSecondaryToken(request *SecondaryTokenRequest) (*SecondaryTokenResponse, error) {
	secondaryTokensURL := "https://accounts.nintendo.com/connect/1.0.0/api/token"

	// Build request body.
	body := map[string]any{
		"client_id":     "71b963c1b7b6d119",
		"session_token": request.SessionToken,
		"grant_type":    "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token",
	}

	jsonBody, err := json.Marshal(body)
	if err != nil {
		return nil, err
	}

	// Create HTTP request.
	req, err := http.NewRequestWithContext(context.Background(), http.MethodPost, secondaryTokensURL, bytes.NewBuffer(jsonBody))
	if err != nil {
		return nil, err
	}

	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Accept", "application/json")
	req.Header.Set("User-Agent", request.UserAgent)
	req.Header.Set("Host", "accounts.nintendo.com")
	req.Header.Set("Connection", "Keep-Alive")

	// Perform request.
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	// Check if the request was successful.
	if resp.StatusCode != 200 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("Secondary token endpoint returned a non-200 status: %s: %s", resp.Status, string(body))
	}

	// Parse and return result.
	var tokenResp SecondaryTokenResponse
	err = json.NewDecoder(resp.Body).Decode(&tokenResp)
	if err != nil {
		return nil, err
	}

	return &tokenResp, nil
}

type UserInfoRequest struct {
	UserAgent   string
	AccessToken string
}

type UserInfoResponse struct {
	CreatedAt  int    `json:"createdAt"`
	Birthday   string `json:"birthday"`
	Gender     string `json:"gender"`
	Id         string `json:"id"`
	Nickname   string `json:"nickname"`
	ScreenName string `json:"screenName"`
	IconUri    string `json:"iconUri"`
}

func GetUserInfo(request *UserInfoRequest) (*UserInfoResponse, error) {
	userInfoURL := "https://api.accounts.nintendo.com/2.0.0/users/me"

	// Create HTTP request.
	req, err := http.NewRequestWithContext(
		context.Background(),
		http.MethodGet,
		userInfoURL,
		nil,
	)
	if err != nil {
		return nil, err
	}

	req.Header.Set("Accept-Language", "en-US")
	req.Header.Set("User-Agent", "NASDKAPI; Android")
	req.Header.Set("Authorization", "Bearer "+request.AccessToken)
	req.Header.Set("Host", "api.accounts.nintendo.com")
	req.Header.Set("Connection", "Keep-Alive")

	// Perform request.
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	// Check if the request was successful.
	if resp.StatusCode != 200 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("User info endpoint returned a non-200 status: %s: %s", resp.Status, string(body))
	}

	// Parse and return result.
	var result UserInfoResponse
	err = json.NewDecoder(resp.Body).Decode(&result)
	if err != nil {
		return nil, err
	}

	return &result, nil
}
