using System;
using System.Collections;
using System.Collections.Generic;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = Managers.EventType;

public class GameManager : MonoBehaviour
{
    public int score;
    public TMP_Text scoreText;
    public float health;
    public TMP_Text healthText;

    private void OnEnable()
    {
        EventSystem<int>.Subscribe(EventType.UPDATE_SCORE, AddScore);
        EventSystem<float>.Subscribe(EventType.UPDATE_HEALTH, DepleteLife);
    }

    private void OnDisable()
    {
        EventSystem<int>.Unsubscribe(EventType.UPDATE_SCORE, AddScore);
        EventSystem<float>.Unsubscribe(EventType.UPDATE_HEALTH, DepleteLife);
    }

    private void AddScore(int _score)
    {
        score += _score;
        scoreText.text = score.ToString();
    }
    
    private void DepleteLife(float _health)
    {
        health -= _health;
        healthText.text = health.ToString("00") + "/100";

        if (health <= 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
