#!/bin/bash

$curl='/usr/bin/curl'
$url = 'https://gateway.sandbox.push.apple.com:2195';
$cert = 'com.silverscreen.ios-APNS_dev.pem';

$ch = curl_init();

curl_setopt($ch, CURLOPT_URL,$url);
curl_setopt($ch, CURLOPT_FOLLOWLOCATION, 1);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_HEADER, 1);
curl_setopt($ch, CURLOPT_HTTPHEADER, array("Content-Type: application/json"));
curl_setopt($ch, CURLOPT_POST, 1);
curl_setopt($ch, CURLOPT_SSLCERT, $cert);
curl_setopt($ch, CURLOPT_SSLCERTPASSWD, "silverscreen");
curl_setopt($ch, CURLOPT_POSTFIELDS, '{"device_tokens": ["a5d178bb6e3446e8d388f5bbd3f29ccd528bc5cbeb24a5d842240b5992e19189"], "aps": {"alert": "test message one!"}}');

$curl_scraped_page = curl_exec($ch);