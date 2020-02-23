# Effortless API Seed Seed for Any Tech Stack

This project lets you create a project which can natively
communicate with your Effortless API, and any other 
technology stack

# Installing the tools

## SSoT.me 
SSoT.me `ssotme` is the dynamic package manager which keeps the code up to date

    > npm install ssotme -g

## Git Fork/Clone
Fork this repository - then clone the project locally


    > git clone git@github.com:you/eapi-anystack-seed project-xyz
    > cd project-xyz


## Connecting 2 Effortless API

To connect your new repository to your effortless API Endpoint, run these commands

    > ssotme -init -name=YourProject -addSetting amqps=amqps://smqPublic:smqPublic@effortlessapi-rmq.ssot.me/YOUR PROJECT-URL 

    > ssotme -build

The code will start to build in the background.
