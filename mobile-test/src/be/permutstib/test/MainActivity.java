package be.permutstib.test;

import android.app.Activity;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.VideoView;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

public final class MainActivity extends Activity {
    private WebView webView;
    private FrameLayout splashView;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private boolean pageReady = false;
    private boolean animationFinished = false;
    private final Runnable splashSafetyTimeout = this::hideSplash;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        FrameLayout root = new FrameLayout(this);
        webView = new WebView(this);
        webView.setBackgroundColor(Color.rgb(246, 248, 250));

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);

        splashView = createSplashView();

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                pageReady = true;
                if (animationFinished) hideSplash();
            }
        });
        webView.setWebChromeClient(new WebChromeClient());
        webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
        root.addView(webView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        root.addView(splashView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        setContentView(root);
        handler.postDelayed(splashSafetyTimeout, 20000);
        webView.loadUrl("https://permut-stib-alpha.onrender.com/?native=1");
    }

    private FrameLayout createSplashView() {
        FrameLayout splash = new FrameLayout(this);
        splash.setBackgroundColor(Color.rgb(245, 251, 248));

        FrameLayout loading = new FrameLayout(this);
        ImageView logo = new ImageView(this);
        logo.setImageResource(R.drawable.logo_csc);
        logo.setScaleType(ImageView.ScaleType.CENTER_INSIDE);
        FrameLayout.LayoutParams logoParams = new FrameLayout.LayoutParams(260, 260, Gravity.CENTER);
        logoParams.bottomMargin = 80;
        loading.addView(logo, logoParams);

        ProgressBar progress = new ProgressBar(this);
        FrameLayout.LayoutParams progressParams = new FrameLayout.LayoutParams(54, 54, Gravity.CENTER);
        progressParams.topMargin = 250;
        loading.addView(progress, progressParams);

        TextView loadingText = new TextView(this);
        loadingText.setText("Chargement de Permut' STIB…");
        loadingText.setTextColor(Color.rgb(23, 49, 42));
        loadingText.setTextSize(16);
        loadingText.setGravity(Gravity.CENTER);
        FrameLayout.LayoutParams textParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER);
        textParams.topMargin = 360;
        textParams.leftMargin = 32;
        textParams.rightMargin = 32;
        loading.addView(loadingText, textParams);
        splash.addView(loading, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        VideoView video = new VideoView(this);
        video.setVideoURI(Uri.parse("android.resource://" + getPackageName() + "/" + R.raw.csc_stib_ouverture));
        video.setOnPreparedListener(player -> {
            player.setLooping(false);
            video.start();
        });
        video.setOnCompletionListener(player -> {
            animationFinished = true;
            video.setVisibility(View.GONE);
            if (pageReady) hideSplash();
        });
        video.setOnErrorListener((player, what, extra) -> {
            animationFinished = true;
            video.setVisibility(View.GONE);
            if (pageReady) hideSplash();
            return true;
        });
        splash.addView(video, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        return splash;
    }

    private void hideSplash() {
        handler.removeCallbacks(splashSafetyTimeout);
        if (splashView == null || splashView.getParent() == null) return;
        splashView.animate().alpha(0f).setDuration(300).withEndAction(() -> {
            ViewGroup parent = (ViewGroup) splashView.getParent();
            if (parent != null) parent.removeView(splashView);
        });
    }

    @Override
    public void onBackPressed() {
        if (webView != null && webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }

    @Override
    protected void onDestroy() {
        handler.removeCallbacksAndMessages(null);
        if (webView != null) webView.destroy();
        super.onDestroy();
    }
}
