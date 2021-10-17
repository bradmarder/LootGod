import { useState } from 'react';
import './App.css';
import { Alert, Button, Accordion } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = window.location.protocol + '//' + window.location.hostname + ':5000';

export default function Loots(props: IContext) {

    const [isLoading, setIsLoading] = useState(false);

    const deleteLoot = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/DeleteLoot?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const grantLootRequest = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/GrantLootRequest?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const items = props.loots.filter(x => props.spell ? x.isSpell : !x.isSpell);

    return (
        <>
            <h3>Available {(props.spell ? 'Spells' : 'Loots')}</h3>
            {items.length === 0 &&
                <Alert variant='warning'>
                    Looks like there aren't any {(props.spell ? 'spells' : 'loots')} available right now
                </Alert>
            }
            {items.length > 0 &&
                <Accordion>
                    {items.map((item, i) =>
                        <Accordion.Item key={item.id} eventKey={i.toString()}>
                            <Accordion.Header>{item.name} | {item.quantity} available | {props.requests.filter(x => x.lootId === item.id).length} request(s)</Accordion.Header>
                            <Accordion.Body>
                                {props.isAdmin &&
                                    <>
                                        <Button variant='danger' disabled={isLoading} onClick={() => deleteLoot(item.id)}>Delete "{item.name}" and all {props.requests.filter(x => x.lootId === item.id).length} request(s)</Button>
                                        <hr />
                                    </>
                                }
                                {props.requests.filter(x => x.lootId === item.id).map(req =>
                                    <span key={req.id}>
                                        <strong>{req.mainName}</strong> | {req.characterName} | {req.isAlt ? 'alt' : 'main'} | {classes[req.class as any]} | {req.spell || req.quantity}
                                        &nbsp;
                                        <Button variant={'success'} disabled={true} onClick={() => grantLootRequest(item.id)}>Grant</Button>
                                        <br />
                                    </span>
                                )}
                            </Accordion.Body>
                        </Accordion.Item>
                    )}
                </Accordion>
            }
        </>
    );
}
